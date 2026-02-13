using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.MigrateCorrespondence;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class MigrateCorrespondenceHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<IDialogportenService> _dialogportenServiceMock;
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
        private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
        private readonly Mock<ILogger<MigrateCorrespondenceHandler>> _loggerMock;
        private readonly Mock<ICorrespondenceDeleteEventRepository> _correspondenceDeleteRepositoryMock;
        private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock;
        private readonly Mock<ICorrespondenceNotificationRepository> _correspondenceNotificationRepositoryMock;
        private readonly Mock<ICorrespondenceForwardingEventRepository> _correspondenceForwardingEventRepositoryMock;
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
        private readonly Mock<IAttachmentRepository> _attachmentRepositoryMock;
        private readonly Mock<IAttachmentStatusRepository> _attachmentStatusRepositoryMock;
        private readonly Mock<IDialogportenService> _dialogportenServiceForHelperMock;
        private readonly Mock<ILogger<CorrespondenceMigrationEventHelper>> _eventHelperLoggerMock;
        private readonly MigrateCorrespondenceHandler _handler;

        private Guid _defaultUserPartyUuid = Guid.NewGuid();
        private string _defaultUserPartySSN = "12018012345";
        private string _defaultUserPartyIdentifier = $"{UrnConstants.PersonIdAttribute}:{12018012345}";

        public MigrateCorrespondenceHandlerTests()
        {
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _correspondenceDeleteRepositoryMock = new Mock<ICorrespondenceDeleteEventRepository>();            
            _dialogportenServiceMock = new Mock<IDialogportenService>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _hostEnvironmentMock = new Mock<IHostEnvironment>();
            _hostEnvironmentMock.Setup(x => x.EnvironmentName).Returns(Environments.Development);
            _loggerMock = new Mock<ILogger<MigrateCorrespondenceHandler>>();
            var mockCache = new Mock<IHybridCacheWrapper>();

            // Setup mocks for CorrespondenceEventHelper dependencies
            _correspondenceStatusRepositoryMock = new Mock<ICorrespondenceStatusRepository>();
            _correspondenceNotificationRepositoryMock = new Mock<ICorrespondenceNotificationRepository>();
            _correspondenceForwardingEventRepositoryMock = new Mock<ICorrespondenceForwardingEventRepository>();
            _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
            _attachmentRepositoryMock = new Mock<IAttachmentRepository>();
            _attachmentStatusRepositoryMock = new Mock<IAttachmentStatusRepository>();            
            _dialogportenServiceForHelperMock = new Mock<IDialogportenService>();
            _eventHelperLoggerMock = new Mock<ILogger<CorrespondenceMigrationEventHelper>>();
            
            var purgeCorrespondenceHelper = new PurgeCorrespondenceHelper(
                _attachmentRepositoryMock.Object,
                _attachmentStatusRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _backgroundJobClientMock.Object,
                _dialogportenServiceForHelperMock.Object,
                _correspondenceRepositoryMock.Object);

            var correspondenceEventHelper = new CorrespondenceMigrationEventHelper(
                _correspondenceStatusRepositoryMock.Object,
                _correspondenceDeleteRepositoryMock.Object,
                _correspondenceNotificationRepositoryMock.Object,
                _correspondenceForwardingEventRepositoryMock.Object,
                _altinnRegisterServiceMock.Object,
                purgeCorrespondenceHelper,
                _backgroundJobClientMock.Object,
                _eventHelperLoggerMock.Object);

            // Ensure Create returns a non-null job id by default (needed for continuations)
            _backgroundJobClientMock
                .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
                .Returns(() => Guid.NewGuid().ToString());

            var hangfireScheduleHelper = new HangfireScheduleHelper(_backgroundJobClientMock.Object, mockCache.Object, _correspondenceRepositoryMock.Object, new NullLogger<HangfireScheduleHelper>());
            _handler = new MigrateCorrespondenceHandler(
                _correspondenceRepositoryMock.Object,
                _dialogportenServiceMock.Object,
                hangfireScheduleHelper,
                _backgroundJobClientMock.Object,
                _hostEnvironmentMock.Object,
                correspondenceEventHelper,
                _loggerMock.Object);
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithAsyncProcessingFalse_ShouldProcessSynchronously()
        {
            // Arrange
            var request = new MakeCorrespondenceAvailableRequest
            {
                BatchSize = 5,
                AsyncProcessing = false,
                CreateEvents = false
            };

            var mockCorrespondences = new List<CorrespondenceEntity>
            {
                CreateMockCorrespondence(Guid.NewGuid()),
                CreateMockCorrespondence(Guid.NewGuid()),
                CreateMockCorrespondence(Guid.NewGuid()),
                CreateMockCorrespondence(Guid.NewGuid()),
                CreateMockCorrespondence(Guid.NewGuid())
            };

            _correspondenceRepositoryMock.Setup(x => x.GetCandidatesForMigrationToDialogporten(
                It.IsAny<int>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockCorrespondences);

            // Setup GetCorrespondenceById for each correspondence since MakeCorrespondenceAvailableInDialogportenAndApi calls it
            foreach (var correspondence in mockCorrespondences)
            {
                _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                    correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                    .ReturnsAsync(correspondence);
            }

            _dialogportenServiceMock.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                It.IsAny<Guid>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync("dialog-123");

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);
            var response = result.AsT0;
            Assert.NotNull(response.Statuses);
            Assert.Equal(5, response.Statuses.Count);

            // Verify that repository was called to get correspondences
            _correspondenceRepositoryMock.Verify(x => x.GetCandidatesForMigrationToDialogporten(
                request.BatchSize.Value,
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // Verify that no background jobs were scheduled
            _backgroundJobClientMock.Verify(x => x.Create(
                It.IsAny<Job>(),
                It.IsAny<IState>()), Times.Never);
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithCorrespondenceId_ShouldProcessSingleCorrespondence()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var request = new MakeCorrespondenceAvailableRequest
            {
                CorrespondenceId = correspondenceId,
                CreateEvents = true
            };

            var mockCorrespondence = CreateMockCorrespondence(correspondenceId);
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockCorrespondence);

            _dialogportenServiceMock.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, mockCorrespondence, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync("dialog-123");

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);
            var response = result.AsT0;
            Assert.NotNull(response.Statuses);
            Assert.Single(response.Statuses);
            Assert.Equal(correspondenceId, response.Statuses[0].CorrespondenceId);
            Assert.Equal("dialog-123", response.Statuses[0].DialogId);
            Assert.True(response.Statuses[0].Ok);
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithCorrespondenceId_CorrespondenceIsPurged_ShouldNotMakeAvailable()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var request = new MakeCorrespondenceAvailableRequest
            {
                CorrespondenceId = correspondenceId,
                CreateEvents = true
            };

            var mockCorrespondence = CreateMockCorrespondence(correspondenceId);
            mockCorrespondence.Statuses.Add(new CorrespondenceStatusEntity
            {
                Status = Core.Models.Enums.CorrespondenceStatus.PurgedByRecipient,
                StatusChanged = DateTimeOffset.UtcNow.AddDays(-1)
            });
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockCorrespondence);            

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);
            var response = result.AsT0;
            Assert.NotNull(response.Statuses);
            Assert.Single(response.Statuses);
            Assert.Equal(correspondenceId, response.Statuses[0].CorrespondenceId);
            Assert.False(response.Statuses[0].Ok);

            _dialogportenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithCorrespondenceId_CorrespondenceBelongsToSelfIdentifiedRecipient_ShouldNotMakeAvailable()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var request = new MakeCorrespondenceAvailableRequest
            {
                CorrespondenceId = correspondenceId,
                CreateEvents = true
            };

            var mockCorrespondence = CreateMockCorrespondence(correspondenceId);
            mockCorrespondence.Recipient = "urn:altinn:party:uuid:96B337F1-05EB-4DF0-A83A-165C14558153";

            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockCorrespondence);

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);
            var response = result.AsT0;
            Assert.NotNull(response.Statuses);
            Assert.Single(response.Statuses);
            Assert.Equal(correspondenceId, response.Statuses[0].CorrespondenceId);
            Assert.False(response.Statuses[0].Ok);

            _dialogportenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithCorrespondenceIds_ShouldProcessMultipleCorrespondences()
        {
            // Arrange
            var correspondenceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var request = new MakeCorrespondenceAvailableRequest
            {
                CorrespondenceIds = correspondenceIds,
                CreateEvents = false
            };

            var mockCorrespondences = correspondenceIds.Select(id => CreateMockCorrespondence(id)).ToList();
            
            foreach (var correspondence in mockCorrespondences)
            {
                _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                    correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                    .ReturnsAsync(correspondence);

                _dialogportenServiceMock.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                    correspondence.Id, correspondence, It.IsAny<bool>(), It.IsAny<bool>()))
                    .ReturnsAsync($"dialog-{correspondence.Id}");
            }

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);
            var response = result.AsT0;
            Assert.NotNull(response.Statuses);
            Assert.Equal(3, response.Statuses.Count);

            foreach (var status in response.Statuses)
            {
                Assert.True(status.Ok);
                Assert.NotNull(status.DialogId);
                Assert.Equal($"dialog-{status.CorrespondenceId}", status.DialogId);
            }
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithCorrespondenceIds_IsArchived()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var request = new MakeCorrespondenceAvailableRequest
            {
                CorrespondenceIds = new List<Guid>() { correspondenceId },
                CreateEvents = false
            };

            var correspondence =  CreateMockCorrespondence(correspondenceId);
            correspondence.Statuses.Add(new CorrespondenceStatusEntity
            {
                Status = Core.Models.Enums.CorrespondenceStatus.Archived,
                StatusChanged = DateTimeOffset.UtcNow.AddDays(-1),
                StatusText = "Archived In Altinn 2"
            });
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondence);

            _dialogportenServiceMock.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondence.Id, correspondence, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondence.Id}");

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);
            var response = result.AsT0;
            Assert.NotNull(response.Statuses);
            Assert.Equal(1, response.Statuses.Count);

            foreach (var status in response.Statuses)
            {
                Assert.True(status.Ok);
                Assert.NotNull(status.DialogId);
                Assert.Equal($"dialog-{status.CorrespondenceId}", status.DialogId);
            }
            _dialogportenServiceMock.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondence.Id, correspondence, false, It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithCorrespondenceIds_IsSoftDeleted()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var request = new MakeCorrespondenceAvailableRequest
            {
                CorrespondenceIds = new List<Guid>() { correspondenceId },
                CreateEvents = false
            };

            var correspondence = CreateMockCorrespondence(correspondenceId);
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondence);

            _dialogportenServiceMock.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondence.Id, correspondence, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondence.Id}");

            _correspondenceDeleteRepositoryMock.Setup(x => x.GetDeleteEventsForCorrespondenceId(
                correspondence.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        CorrespondenceId = correspondence.Id,
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1),
                        EventType = Core.Models.Enums.CorrespondenceDeleteEventType.SoftDeletedByRecipient,
                        SyncedFromAltinn2 = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(1),
                    }
                });

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);
            var response = result.AsT0;
            Assert.NotNull(response.Statuses);
            Assert.Equal(1, response.Statuses.Count);

            foreach (var status in response.Statuses)
            {
                Assert.True(status.Ok);
                Assert.NotNull(status.DialogId);
                Assert.Equal($"dialog-{status.CorrespondenceId}", status.DialogId);
            }
            _dialogportenServiceMock.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondence.Id, correspondence, It.IsAny<bool>(), true), Times.Once);
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithCorrespondenceIds_IsArchivedAndSoftDeleted()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var request = new MakeCorrespondenceAvailableRequest
            {
                CorrespondenceIds = new List<Guid>() { correspondenceId },
                CreateEvents = false
            };

            var correspondence = CreateMockCorrespondence(correspondenceId);
            correspondence.Statuses.Add(new CorrespondenceStatusEntity
            {
                Status = Core.Models.Enums.CorrespondenceStatus.Archived,
                StatusChanged = DateTimeOffset.UtcNow.AddDays(-1),
                StatusText = "Archived In Altinn 2"
            });

            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondence);

            _dialogportenServiceMock.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondence.Id, correspondence, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondence.Id}");

            _correspondenceDeleteRepositoryMock.Setup(x => x.GetDeleteEventsForCorrespondenceId(
                correspondence.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        CorrespondenceId = correspondence.Id,
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1),
                        EventType = Core.Models.Enums.CorrespondenceDeleteEventType.SoftDeletedByRecipient,
                        SyncedFromAltinn2 = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(1),
                    }
                });

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);
            var response = result.AsT0;
            Assert.NotNull(response.Statuses);
            Assert.Equal(1, response.Statuses.Count);

            foreach (var status in response.Statuses)
            {
                Assert.True(status.Ok);
                Assert.NotNull(status.DialogId);
                Assert.Equal($"dialog-{status.CorrespondenceId}", status.DialogId);
            }
            _dialogportenServiceMock.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondence.Id, correspondence, It.IsAny<bool>(), true), Times.Once);
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithException_ShouldHandleErrorGracefully()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var request = new MakeCorrespondenceAvailableRequest
            {
                CorrespondenceId = correspondenceId,
                CreateEvents = false
            };

            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);
            var response = result.AsT0;
            Assert.NotNull(response.Statuses);
            Assert.Single(response.Statuses);
            Assert.Equal(correspondenceId, response.Statuses[0].CorrespondenceId);
            Assert.False(response.Statuses[0].Ok);
            Assert.NotNull(response.Statuses[0].Error);
            Assert.Contains("Test exception", response.Statuses[0].Error);
        }

        [Fact]
        public async Task ProcessAndMakeAvailable_Read_OK()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            int altinn2CorrespondenceId = 12345;
            var correspondenceRequestObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithId(Guid.Empty) // Not set before creation
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)                    
                    .WithStatus(CorrespondenceStatus.Published)
                    .WithStatus(CorrespondenceStatus.Read)                    
                    .Build();

            var correspondenceMockReturn = new CorrespondenceEntity
            {
                Id = correspondenceId,
                ResourceId = correspondenceRequestObject.ResourceId,
                Recipient = correspondenceRequestObject.Recipient,
                Sender = correspondenceRequestObject.Sender,
                SendersReference = correspondenceRequestObject.SendersReference,
                RequestedPublishTime = correspondenceRequestObject.RequestedPublishTime,
                Statuses = correspondenceRequestObject.Statuses,
                ExternalReferences = correspondenceRequestObject.ExternalReferences,
                Created = correspondenceRequestObject.Created,
                Altinn2CorrespondenceId = correspondenceRequestObject.Altinn2CorrespondenceId
            };

            var request = new MigrateCorrespondenceRequest
            {
                Altinn2CorrespondenceId = altinn2CorrespondenceId,
                CorrespondenceEntity = correspondenceRequestObject,
                MakeAvailable = true
            };

            _correspondenceRepositoryMock.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceMockReturn);
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(
                correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondenceMockReturn);

            _dialogportenServiceMock.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondenceId}");

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _correspondenceRepositoryMock.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Published) &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Read)
            ), It.IsAny<CancellationToken>()), Times.Once);

            _correspondenceRepositoryMock.Verify(x => x.AddExternalReference(
                correspondenceId, ReferenceType.DialogportenDialogId, $"dialog-{correspondenceId}", It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.Verify(x => x.UpdateIsMigrating(
                correspondenceId, false, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _dialogportenServiceMock.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), false), Times.Once);
            _dialogportenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ProcessAndMakeAvailable_ReadAndSoftDeleted_OK()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            int altinn2CorrespondenceId = 12345;
            var correspondenceRequestObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithId(Guid.Empty) // Not set before creation
                    .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow.AddDays(-1))
                    .WithStatus(CorrespondenceStatus.Read, DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(5))
                    .Build();
            var correspondenceMockReturn = new CorrespondenceEntity
            {
                Id = correspondenceId,
                ResourceId = correspondenceRequestObject.ResourceId,
                Recipient = correspondenceRequestObject.Recipient,
                Sender = correspondenceRequestObject.Sender,
                SendersReference = correspondenceRequestObject.SendersReference,
                RequestedPublishTime = correspondenceRequestObject.RequestedPublishTime,
                Statuses = correspondenceRequestObject.Statuses,
                ExternalReferences = correspondenceRequestObject.ExternalReferences,
                Created = correspondenceRequestObject.Created,
                Altinn2CorrespondenceId = correspondenceRequestObject.Altinn2CorrespondenceId
            };

            var request = new MigrateCorrespondenceRequest
            {
                Altinn2CorrespondenceId = altinn2CorrespondenceId,
                CorrespondenceEntity = correspondenceRequestObject,
                DeleteEventEntities = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        CorrespondenceId = correspondenceId,
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1),
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient
                    }
                },
                MakeAvailable = true
            };

            _correspondenceRepositoryMock.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceMockReturn);
            _correspondenceDeleteRepositoryMock.Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CorrespondenceDeleteEventEntity e, CancellationToken _) => e);
            _correspondenceDeleteRepositoryMock.Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        CorrespondenceId = correspondenceId,
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1),
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient,
                        SyncedFromAltinn2 = DateTimeOffset.UtcNow,
                    }
                });
            _dialogportenServiceMock.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondenceId}");

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _correspondenceRepositoryMock.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Published) &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Read)
            ), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.Verify(x => x.AddExternalReference(
               correspondenceId, ReferenceType.DialogportenDialogId, $"dialog-{correspondenceId}", It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.Verify(x => x.UpdateIsMigrating(
                correspondenceId, false, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteRepositoryMock.Verify(x => x.AddDeleteEvent(It.Is<CorrespondenceDeleteEventEntity>(e =>
                e.CorrespondenceId == correspondenceId &&
                e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient &&
                e.EventOccurred == request.DeleteEventEntities[0].EventOccurred &&
                e.SyncedFromAltinn2 != null
            ), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);

            _dialogportenServiceMock.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), true), Times.Once);
            _dialogportenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ProcessAndMakeAvailable_SoftDeletedAndRestoredMultiple_OK()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            int altinn2CorrespondenceId = 12345;
            var correspondenceRequestObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithId(Guid.Empty) // Not set before creation
                    .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow.AddDays(-1))
                    .WithStatus(CorrespondenceStatus.Read, DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(5))
                    .Build();
            var correspondenceMockReturn = new CorrespondenceEntity
            {
                Id = correspondenceId,
                ResourceId = correspondenceRequestObject.ResourceId,
                Recipient = correspondenceRequestObject.Recipient,
                Sender = correspondenceRequestObject.Sender,
                SendersReference = correspondenceRequestObject.SendersReference,
                RequestedPublishTime = correspondenceRequestObject.RequestedPublishTime,
                Statuses = correspondenceRequestObject.Statuses,
                ExternalReferences = correspondenceRequestObject.ExternalReferences,
                Created = correspondenceRequestObject.Created,
                Altinn2CorrespondenceId = correspondenceRequestObject.Altinn2CorrespondenceId
            };
            var request = new MigrateCorrespondenceRequest
            {
                Altinn2CorrespondenceId = altinn2CorrespondenceId,
                CorrespondenceEntity = correspondenceRequestObject,
                DeleteEventEntities = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(6),
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient
                    },
                    new CorrespondenceDeleteEventEntity
                    {
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(7),
                        EventType = CorrespondenceDeleteEventType.RestoredByRecipient
                    },
                    new CorrespondenceDeleteEventEntity
                    {
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(8),
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient
                    },
                },
                MakeAvailable = true
            };

            _correspondenceRepositoryMock.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceMockReturn);
            _correspondenceDeleteRepositoryMock.Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CorrespondenceDeleteEventEntity e, CancellationToken _) => e);
            _correspondenceDeleteRepositoryMock.Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        CorrespondenceId = correspondenceId,
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(6),
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient,
                        SyncedFromAltinn2 = DateTimeOffset.UtcNow,
                    },
                    new CorrespondenceDeleteEventEntity
                    {
                        CorrespondenceId = correspondenceId,
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(7),
                        EventType = CorrespondenceDeleteEventType.RestoredByRecipient,
                        SyncedFromAltinn2 = DateTimeOffset.UtcNow,
                    },
                    new CorrespondenceDeleteEventEntity
                    {
                        CorrespondenceId = correspondenceId,
                        EventOccurred = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(8),
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient,
                        SyncedFromAltinn2 = DateTimeOffset.UtcNow,
                    },
                });
            _dialogportenServiceMock.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondenceId}");

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _correspondenceRepositoryMock.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Published) &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Read)
            ), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.Verify(x => x.AddExternalReference(
                 correspondenceId, ReferenceType.DialogportenDialogId, $"dialog-{correspondenceId}", It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.Verify(x => x.UpdateIsMigrating(
                correspondenceId, false, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteRepositoryMock.Verify(x => x.AddDeleteEvent(It.Is<CorrespondenceDeleteEventEntity>(e =>
                e.CorrespondenceId == correspondenceId &&
                e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient &&
                e.EventOccurred == request.DeleteEventEntities[0].EventOccurred &&
                e.SyncedFromAltinn2 != null
            ), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteRepositoryMock.Verify(x => x.AddDeleteEvent(It.Is<CorrespondenceDeleteEventEntity>(e =>
                e.CorrespondenceId == correspondenceId &&
                e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient &&
                e.EventOccurred == request.DeleteEventEntities[1].EventOccurred &&
                e.SyncedFromAltinn2 != null
            ), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteRepositoryMock.Verify(x => x.AddDeleteEvent(It.Is<CorrespondenceDeleteEventEntity>(e =>
                e.CorrespondenceId == correspondenceId &&
                e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient &&
                e.EventOccurred == request.DeleteEventEntities[2].EventOccurred &&
                e.SyncedFromAltinn2 != null
            ), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteRepositoryMock.VerifyNoOtherCalls();

            _dialogportenServiceMock.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), true), Times.Once);
            _dialogportenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Remigrate_NewEventsInA3_NothingChanged()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            int altinn2CorrespondenceId = 12345;
            var correspondenceRequestObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithRecipient(_defaultUserPartyIdentifier)
                    .WithDialogId("dialog-id-123")
                    .WithId(correspondenceId)
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithStatus(CorrespondenceStatus.Published)
                    .WithStatus(CorrespondenceStatus.Read)
                    .Build();

            var correspondenceExistingObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithRecipient(_defaultUserPartyIdentifier)
                    .WithDialogId("dialog-id-123")
                    .WithId(correspondenceId)
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithStatus(CorrespondenceStatus.Published)
                    .WithStatus(CorrespondenceStatus.Read)
                    .WithStatus(CorrespondenceStatus.Confirmed)
                    .Build();

            var request = new MigrateCorrespondenceRequest
            {
                Altinn2CorrespondenceId = altinn2CorrespondenceId,
                CorrespondenceEntity = correspondenceRequestObject,
                MakeAvailable = true
            };

            _correspondenceRepositoryMock.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .Throws(new DbUpdateException("An error occurred while updating the entries.",
                    new Npgsql.PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505")));
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CorrespondenceEntity
                {
                    Id = correspondenceId,
                    ResourceId = correspondenceExistingObject.ResourceId,
                    Recipient = correspondenceExistingObject.Recipient,
                    Sender = correspondenceExistingObject.Sender,
                    SendersReference = correspondenceExistingObject.SendersReference,
                    RequestedPublishTime = correspondenceExistingObject.RequestedPublishTime,
                    Statuses = correspondenceExistingObject.Statuses,
                    ExternalReferences = correspondenceExistingObject.ExternalReferences,
                    Created = correspondenceExistingObject.Created,
                    Altinn2CorrespondenceId = correspondenceExistingObject.Altinn2CorrespondenceId
                });
            _correspondenceRepositoryMock.Setup(x => x.ClearChangeTracker());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _correspondenceRepositoryMock.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId
            ), It.IsAny<CancellationToken>()), Times.Once);

            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.Verify(x => x.ClearChangeTracker(), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
            _correspondenceForwardingEventRepositoryMock.VerifyNoOtherCalls();
            _dialogportenServiceMock.VerifyNoOtherCalls();
            _backgroundJobClientMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Remigrate_AllDuplicates_NothingAdded()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            int altinn2CorrespondenceId = 12345;
            var correspondenceRequestObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithRecipient(_defaultUserPartyIdentifier)
                    .WithDialogId("dialog-id-123")
                    .WithId(correspondenceId)
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithStatus(CorrespondenceStatus.Published)
                    .WithStatus(CorrespondenceStatus.Read)
                    .Build();

            var request = new MigrateCorrespondenceRequest
            {
                Altinn2CorrespondenceId = altinn2CorrespondenceId,
                CorrespondenceEntity = correspondenceRequestObject,
                MakeAvailable = true
            };

            _correspondenceRepositoryMock.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .Throws(new DbUpdateException("An error occurred while updating the entries.", 
                    new Npgsql.PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505")));
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CorrespondenceEntity
                {
                    Id = correspondenceId,
                    ResourceId = correspondenceRequestObject.ResourceId,
                    Recipient = correspondenceRequestObject.Recipient,
                    Sender = correspondenceRequestObject.Sender,
                    SendersReference = correspondenceRequestObject.SendersReference,
                    RequestedPublishTime = correspondenceRequestObject.RequestedPublishTime,
                    Statuses = correspondenceRequestObject.Statuses,
                    ExternalReferences = correspondenceRequestObject.ExternalReferences,
                    Created = correspondenceRequestObject.Created,
                    Altinn2CorrespondenceId = correspondenceRequestObject.Altinn2CorrespondenceId
                });
            _correspondenceRepositoryMock.Setup(x => x.ClearChangeTracker());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _correspondenceRepositoryMock.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId
            ), It.IsAny<CancellationToken>()), Times.Once);

            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.Verify(x => x.ClearChangeTracker(), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
            _correspondenceForwardingEventRepositoryMock.VerifyNoOtherCalls();
            _dialogportenServiceMock.VerifyNoOtherCalls();
            _backgroundJobClientMock.VerifyNoOtherCalls();            
        }

        [Fact]
        public async Task Remigrate_NewReadAndConfirmed_OK()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            int altinn2CorrespondenceId = 12345;
            var correspondenceRequestObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithRecipient(_defaultUserPartyIdentifier)
                    .WithDialogId("dialog-id-123")
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithStatus(CorrespondenceStatus.Published, new DateTime(2025,12,10,10,00,00), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Read, new DateTime(2025, 12, 10, 10, 05, 00), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Confirmed, new DateTime(2025, 12, 10, 10, 05, 10), _defaultUserPartyUuid)
                    .Build();


            var correspondenceExistingObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithRecipient(_defaultUserPartyIdentifier)
                    .WithDialogId("dialog-id-123")
                    .WithId(correspondenceId)
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithStatus(CorrespondenceStatus.Published, new DateTime(2025, 12, 10, 10, 00, 00), _defaultUserPartyUuid)
                    .Build();

            var request = new MigrateCorrespondenceRequest
            {
                Altinn2CorrespondenceId = altinn2CorrespondenceId,
                CorrespondenceEntity = correspondenceRequestObject,
                MakeAvailable = true
            };

            _correspondenceRepositoryMock.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .Throws(new DbUpdateException("An error occurred while updating the entries.",
                    new Npgsql.PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505")));
            _correspondenceRepositoryMock.Setup(x => x.ClearChangeTracker());
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CorrespondenceEntity
                {
                    Id = correspondenceId,
                    ResourceId = correspondenceExistingObject.ResourceId,
                    Recipient = correspondenceExistingObject.Recipient,
                    Sender = correspondenceExistingObject.Sender,
                    SendersReference = correspondenceExistingObject.SendersReference,
                    RequestedPublishTime = correspondenceExistingObject.RequestedPublishTime,
                    Statuses = correspondenceExistingObject.Statuses,
                    ExternalReferences = correspondenceExistingObject.ExternalReferences,
                    Created = correspondenceExistingObject.Created,
                    Altinn2CorrespondenceId = correspondenceExistingObject.Altinn2CorrespondenceId
                });

            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatus(It.IsAny<CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.NewGuid());
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyByPartyUuid(_defaultUserPartyUuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = _defaultUserPartyUuid, SSN = _defaultUserPartySSN, PartyTypeName = PartyType.Person });

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _correspondenceRepositoryMock.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId
            ), It.IsAny<CancellationToken>()), Times.Once);

            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()), Times.Once);

            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.Read &&
                    e.StatusChanged == new DateTime(2025, 12, 10, 10, 05, 00) &&
                    e.PartyUuid == _defaultUserPartyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.Confirmed &&
                    e.StatusChanged == new DateTime(2025, 12, 10, 10, 05, 10) &&
                    e.PartyUuid == _defaultUserPartyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify background job for Altinn Events
            VerifyAltinnEventEnqueued(correspondenceId, AltinnEventType.CorrespondenceReceiverRead, correspondenceExistingObject.Sender);
            VerifyAltinnEventEnqueued(correspondenceId, AltinnEventType.CorrespondenceReceiverConfirmed, correspondenceExistingObject.Sender);
            // Verify background jobs Dialogporten activities
            VerifyDialogportenServicePatchCorrespondenceDialogToConfirmedEnqueued(correspondenceId);
            VerifyDialogportenServiceCreateConfirmedActivityEnqueued(correspondenceId, DialogportenActorType.Recipient, _defaultUserPartyIdentifier);
            VerifyDialogportenServiceCreateOpenedActivityEnqueued(correspondenceId, _defaultUserPartyIdentifier);

            _correspondenceRepositoryMock.Verify(x => x.ClearChangeTracker(), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
            _correspondenceForwardingEventRepositoryMock.VerifyNoOtherCalls();
            _dialogportenServiceMock.VerifyNoOtherCalls();
            _backgroundJobClientMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Remigrate_ReadConfirmed_NewArchived_OK()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            int altinn2CorrespondenceId = 12345;
            var correspondenceRequestObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithRecipient(_defaultUserPartyIdentifier)
                    .WithDialogId("dialog-id-123")
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithStatus(CorrespondenceStatus.Published, new DateTime(2025, 12, 10, 10, 00, 00), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Read, new DateTime(2025, 12, 10, 10, 05, 00), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Confirmed, new DateTime(2025, 12, 10, 10, 05, 10), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Archived, new DateTime(2025, 12, 10, 10, 10, 10), _defaultUserPartyUuid)
                    .Build();


            var correspondenceExistingObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithRecipient(_defaultUserPartyIdentifier)
                    .WithDialogId("dialog-id-123")
                    .WithId(correspondenceId)
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithStatus(CorrespondenceStatus.Published, new DateTime(2025, 12, 10, 10, 00, 00), _defaultUserPartyUuid)
                     .WithStatus(CorrespondenceStatus.Read, new DateTime(2025, 12, 10, 10, 05, 00), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Confirmed, new DateTime(2025, 12, 10, 10, 05, 10), _defaultUserPartyUuid)                    
                    .Build();

            var request = new MigrateCorrespondenceRequest
            {
                Altinn2CorrespondenceId = altinn2CorrespondenceId,
                CorrespondenceEntity = correspondenceRequestObject,
                MakeAvailable = true
            };

            _correspondenceRepositoryMock.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .Throws(new DbUpdateException("An error occurred while updating the entries.",
                    new Npgsql.PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505")));
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CorrespondenceEntity
                {
                    Id = correspondenceId,
                    ResourceId = correspondenceExistingObject.ResourceId,
                    Recipient = correspondenceExistingObject.Recipient,
                    Sender = correspondenceExistingObject.Sender,
                    SendersReference = correspondenceExistingObject.SendersReference,
                    RequestedPublishTime = correspondenceExistingObject.RequestedPublishTime,
                    Statuses = correspondenceExistingObject.Statuses,
                    ExternalReferences = correspondenceExistingObject.ExternalReferences,
                    Created = correspondenceExistingObject.Created,
                    Altinn2CorrespondenceId = correspondenceExistingObject.Altinn2CorrespondenceId
                });
            _correspondenceRepositoryMock.Setup(x => x.ClearChangeTracker());

            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatus(It.IsAny<CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.NewGuid());
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyByPartyUuid(_defaultUserPartyUuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = _defaultUserPartyUuid, SSN = _defaultUserPartySSN, PartyTypeName = PartyType.Person });

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _correspondenceRepositoryMock.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId
            ), It.IsAny<CancellationToken>()), Times.Once);

            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()), Times.Once);

            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.Archived &&
                    e.StatusChanged == new DateTime(2025, 12, 10, 10, 10, 10) &&
                    e.PartyUuid == _defaultUserPartyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            VerifyDialogportenServiceSetArchivedSystemLabelOnDialogEnqueued(correspondenceId, _defaultUserPartyIdentifier);

            _correspondenceRepositoryMock.Verify(x => x.ClearChangeTracker(), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
            _correspondenceForwardingEventRepositoryMock.VerifyNoOtherCalls();
            _dialogportenServiceMock.VerifyNoOtherCalls();
            _backgroundJobClientMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Remigrate_NewAndDuplicateOfAllTypes_NewAdded()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            int altinn2CorrespondenceId = 12345;
            var correspondenceRequestObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithRecipient(_defaultUserPartyIdentifier)
                    .WithDialogId("dialog-id-123")
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithStatus(CorrespondenceStatus.Published, new DateTime(2025, 12, 10, 10, 00, 00), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Read, new DateTime(2025, 12, 10, 10, 05, 00), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Confirmed, new DateTime(2025, 12, 10, 10, 05, 10), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Archived, new DateTime(2025, 12, 10, 10, 10, 10), _defaultUserPartyUuid)
                    .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>
                        {
                            new CorrespondenceForwardingEventEntity
                            {
                                // Example of Copy sent to own email address
                                ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                                ForwardedByPartyUuid = _defaultUserPartyUuid,
                                ForwardedByUserId = 123,
                                ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                                ForwardedToEmailAddress = "user1@awesometestusers.com",
                                ForwardingText = "Keep this as a backup in my email."
                            },
                            new CorrespondenceForwardingEventEntity
                            {
                                // Example of Copy sent to own digital mailbox
                                ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 5, 0)),
                                ForwardedByPartyUuid = _defaultUserPartyUuid,
                                ForwardedByUserId = 123,
                                ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                                MailboxSupplier = "urn:altinn:organization:identifier-no:123456789"
                            },
                            new CorrespondenceForwardingEventEntity
                            {
                                // Example of Instance Delegation by User 1 to User2
                                ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15, 0)),
                                ForwardedByPartyUuid = _defaultUserPartyUuid,
                                ForwardedByUserId = 123,
                                ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                                ForwardedToUserId = 456,
                                ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
                                ForwardingText = "User2, - look into this for me please. - User1.",
                                ForwardedToEmailAddress  = "user2@awesometestusers.com"
                            }
                        })
                    .WithSingleAltinn2Notification(2, "testemail@altinn.no", NotificationChannel.Email, new DateTime(2024, 1, 7), new DateTime(2024, 1, 14), true)
                    .Build();

            var correspondenceExistingObject = new CorrespondenceEntityBuilder()
                    .WithResourceId("TTD-migratedCorrespondence-1-1")
                    .WithRecipient(_defaultUserPartyIdentifier)
                    .WithDialogId("dialog-id-123")
                    .WithId(correspondenceId)
                    .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                    .WithStatus(CorrespondenceStatus.Published, new DateTime(2025, 12, 10, 10, 00, 00), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Read, new DateTime(2025, 12, 10, 10, 05, 00), _defaultUserPartyUuid)
                    .WithStatus(CorrespondenceStatus.Confirmed, new DateTime(2025, 12, 10, 10, 05, 10), _defaultUserPartyUuid)
                    .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>
                        {
                            new CorrespondenceForwardingEventEntity
                            {
                                // Example of Copy sent to own email address
                                ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                                ForwardedByPartyUuid = _defaultUserPartyUuid,
                                ForwardedByUserId = 123,
                                ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                                ForwardedToEmailAddress = "user1@awesometestusers.com",
                                ForwardingText = "Keep this as a backup in my email."
                            }
                        })
                    .WithSingleAltinn2Notification(1, "testemail@altinn.no", NotificationChannel.Email, new DateTime(2024, 1, 7), new DateTime(2024, 1, 7, 12, 0, 0), false)
                    .Build();

            var request = new MigrateCorrespondenceRequest
            {
                Altinn2CorrespondenceId = altinn2CorrespondenceId,
                CorrespondenceEntity = correspondenceRequestObject,
                MakeAvailable = true
            };

            _correspondenceRepositoryMock.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .Throws(new DbUpdateException("An error occurred while updating the entries.",
                    new Npgsql.PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505")));
            _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CorrespondenceEntity
                {
                    Id = correspondenceId,
                    ResourceId = correspondenceExistingObject.ResourceId,
                    Recipient = correspondenceExistingObject.Recipient,
                    Sender = correspondenceExistingObject.Sender,
                    SendersReference = correspondenceExistingObject.SendersReference,
                    RequestedPublishTime = correspondenceExistingObject.RequestedPublishTime,
                    Statuses = correspondenceExistingObject.Statuses,
                    ExternalReferences = correspondenceExistingObject.ExternalReferences,
                    Created = correspondenceExistingObject.Created,
                    Altinn2CorrespondenceId = correspondenceExistingObject.Altinn2CorrespondenceId,
                    ForwardingEvents = correspondenceExistingObject.ForwardingEvents,
                    Notifications = correspondenceExistingObject.Notifications
                });
            _correspondenceRepositoryMock.Setup(x => x.ClearChangeTracker());

            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatus(It.IsAny<CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.NewGuid());
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyByPartyUuid(_defaultUserPartyUuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = _defaultUserPartyUuid, SSN = _defaultUserPartySSN, PartyTypeName = PartyType.Person });

            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);
            _correspondenceForwardingEventRepositoryMock
                .Setup(x => x.AddForwardingEvents(It.IsAny<List<CorrespondenceForwardingEventEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceForwardingEventEntity> events, CancellationToken _) => events);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _correspondenceRepositoryMock.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId
            ), It.IsAny<CancellationToken>()), Times.Once);

            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByAltinn2Id(
                altinn2CorrespondenceId, It.IsAny<CancellationToken>()), Times.Once);

            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.Archived &&
                    e.StatusChanged == new DateTime(2025, 12, 10, 10, 10, 10) &&
                    e.PartyUuid == _defaultUserPartyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceForwardingEventRepositoryMock.Verify(
                x => x.AddForwardingEvents(
                    It.Is<List<CorrespondenceForwardingEventEntity>>(n => n.Count == 2),
                    It.IsAny<CancellationToken>()),
                Times.Once
            );
            _correspondenceForwardingEventRepositoryMock.VerifyNoOtherCalls();

            _correspondenceNotificationRepositoryMock.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n =>
                n.Altinn2NotificationId == 2 && n.SyncedFromAltinn2 != null && n.CorrespondenceId == correspondenceId), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();

            VerifyDialogportenServiceSetArchivedSystemLabelOnDialogEnqueued(correspondenceId, _defaultUserPartyIdentifier);

            _correspondenceRepositoryMock.Verify(x => x.ClearChangeTracker(), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            _dialogportenServiceMock.VerifyNoOtherCalls();
            _backgroundJobClientMock.VerifyNoOtherCalls();
        }

        private static CorrespondenceEntity CreateMockCorrespondence(Guid id)
        {
            return new CorrespondenceEntity
            {
                Id = id,
                ResourceId = "test-resource",
                Recipient = "test-recipient",
                Sender = "0192:123456789",
                SendersReference = "test-reference",
                RequestedPublishTime = DateTimeOffset.UtcNow.AddDays(1),
                Statuses = new List<CorrespondenceStatusEntity>(),
                ExternalReferences = new List<ExternalReferenceEntity>(),
                Created = DateTimeOffset.UtcNow
            };
        }

        private void VerifyAltinnEventEnqueued(Guid correspondenceId, AltinnEventType eventType, string recipient)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IEventBus.Publish) && (AltinnEventType)job.Args[0] == eventType && (string)job.Args[4] == recipient),
                It.IsAny<EnqueuedState>()));
        }

        private void VerifyDialogportenServiceCreateConfirmedActivityEnqueued(Guid correspondenceId, DialogportenActorType actorType, string partyUrn)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.CreateConfirmedActivity) && (Guid)job.Args[0] == correspondenceId && (DialogportenActorType)job.Args[1] == actorType && (string)job.Args[3] == partyUrn),
                It.IsAny<IState>()));
        }

        private void VerifyDialogportenServicePatchCorrespondenceDialogToConfirmedEnqueued(Guid correspondenceId)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.PatchCorrespondenceDialogToConfirmed) && (Guid)job.Args[0] == correspondenceId),
                It.IsAny<EnqueuedState>()));
        }

        private void VerifyDialogportenServiceSetArchivedSystemLabelOnDialogEnqueued(Guid correspondenceId, string partyIdentifier)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                    It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.UpdateSystemLabelsOnDialog)
                        && (Guid)job.Args[0] == correspondenceId
                        && (string)job.Args[1] == partyIdentifier
                        && (DialogportenActorType)job.Args[2] == DialogportenActorType.PartyRepresentative
                        && job.Args[3] != null
                        && ((List<DialogPortenSystemLabel>)job.Args[3]).Contains(DialogPortenSystemLabel.Archive)
                        && job.Args[4] == null),
                    It.IsAny<EnqueuedState>()));
        }

        private void VerifyDialogportenServiceCreateOpenedActivityEnqueued(Guid correspondenceId, string partyUrn)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.CreateOpenedActivity) && (Guid)job.Args[0] == correspondenceId && (string)job.Args[3] == partyUrn),
                It.IsAny<EnqueuedState>()));
        }
    }
}

