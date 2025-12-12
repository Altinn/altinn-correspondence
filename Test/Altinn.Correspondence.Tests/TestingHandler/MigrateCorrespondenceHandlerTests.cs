using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.MigrateCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class MigrateCorrespondenceHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _mockCorrespondenceRepository;
        private readonly Mock<IDialogportenService> _mockDialogportenService;
        private readonly Mock<IBackgroundJobClient> _mockBackgroundJobClient;
        private readonly Mock<IHostEnvironment> _mockHostEnvironment;
        private readonly Mock<ILogger<MigrateCorrespondenceHandler>> _mockLogger;
        private readonly Mock<ICorrespondenceDeleteEventRepository> _mockCorrespondenceDeleteRepository;
        private readonly MigrateCorrespondenceHandler _handler;

        public MigrateCorrespondenceHandlerTests()
        {
            _mockCorrespondenceRepository = new Mock<ICorrespondenceRepository>();
            _mockCorrespondenceDeleteRepository = new Mock<ICorrespondenceDeleteEventRepository>();
            _mockDialogportenService = new Mock<IDialogportenService>();
            _mockBackgroundJobClient = new Mock<IBackgroundJobClient>();
            _mockHostEnvironment = new Mock<IHostEnvironment>();
            _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns(Environments.Development);
            _mockLogger = new Mock<ILogger<MigrateCorrespondenceHandler>>();
            var mockCache = new Mock<IHybridCacheWrapper>();

            // Ensure Create returns a non-null job id by default (needed for continuations)
            _mockBackgroundJobClient
                .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
                .Returns(() => Guid.NewGuid().ToString());

            var hangfireScheduleHelper = new HangfireScheduleHelper(_mockBackgroundJobClient.Object, mockCache.Object, _mockCorrespondenceRepository.Object, new NullLogger<HangfireScheduleHelper>());
            _handler = new MigrateCorrespondenceHandler(
                _mockCorrespondenceRepository.Object,
                _mockCorrespondenceDeleteRepository.Object,
                _mockDialogportenService.Object,
                hangfireScheduleHelper,
                _mockBackgroundJobClient.Object,
                _mockHostEnvironment.Object,
                _mockLogger.Object);
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

            _mockCorrespondenceRepository.Setup(x => x.GetCandidatesForMigrationToDialogporten(
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
                _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
                    correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                    .ReturnsAsync(correspondence);
            }

            _mockDialogportenService.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
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
            _mockCorrespondenceRepository.Verify(x => x.GetCandidatesForMigrationToDialogporten(
                request.BatchSize.Value,
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // Verify that no background jobs were scheduled
            _mockBackgroundJobClient.Verify(x => x.Create(
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
            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
                correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockCorrespondence);

            _mockDialogportenService.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
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
            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
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

            _mockDialogportenService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithCorrespondenceId_CorrespondenceBelongsToSeflidentifiedRecipient_ShouldNotMakeAvailable()
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

            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
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

            _mockDialogportenService.VerifyNoOtherCalls();
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
                _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
                    correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                    .ReturnsAsync(correspondence);

                _mockDialogportenService.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
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
            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
                correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondence);

            _mockDialogportenService.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
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
            _mockDialogportenService.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
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
            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
                correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondence);

            _mockDialogportenService.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondence.Id, correspondence, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondence.Id}");

            _mockCorrespondenceDeleteRepository.Setup(x => x.GetDeleteEventsForCorrespondenceId(
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
            _mockDialogportenService.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
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

            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
                correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondence);

            _mockDialogportenService.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondence.Id, correspondence, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondence.Id}");

            _mockCorrespondenceDeleteRepository.Setup(x => x.GetDeleteEventsForCorrespondenceId(
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
            _mockDialogportenService.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
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

            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
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

            _mockCorrespondenceRepository.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceMockReturn);
            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(
                correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondenceMockReturn);

            _mockDialogportenService.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondenceId}");

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _mockCorrespondenceRepository.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Published) &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Read)
            ), It.IsAny<CancellationToken>()), Times.Once);

            _mockCorrespondenceRepository.Verify(x => x.AddExternalReference(
                correspondenceId, ReferenceType.DialogportenDialogId, $"dialog-{correspondenceId}", It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceRepository.Verify(x => x.UpdateIsMigrating(
                correspondenceId, false, It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceRepository.VerifyNoOtherCalls();

            _mockDialogportenService.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), false), Times.Once);
            _mockDialogportenService.VerifyNoOtherCalls();
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

            _mockCorrespondenceRepository.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceMockReturn);
            _mockCorrespondenceDeleteRepository.Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CorrespondenceDeleteEventEntity e, CancellationToken _) => e);
            _mockCorrespondenceDeleteRepository.Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
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
            _mockDialogportenService.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondenceId}");

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _mockCorrespondenceRepository.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Published) &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Read)
            ), It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceRepository.Verify(x => x.AddExternalReference(
               correspondenceId, ReferenceType.DialogportenDialogId, $"dialog-{correspondenceId}", It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceRepository.Verify(x => x.UpdateIsMigrating(
                correspondenceId, false, It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceRepository.VerifyNoOtherCalls();

            _mockCorrespondenceDeleteRepository.Verify(x => x.AddDeleteEvent(It.Is<CorrespondenceDeleteEventEntity>(e =>
                e.CorrespondenceId == correspondenceId &&
                e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient &&
                e.EventOccurred == request.DeleteEventEntities[0].EventOccurred &&
                e.SyncedFromAltinn2 != null
            ), It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceDeleteRepository.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceRepository.VerifyNoOtherCalls();

            _mockDialogportenService.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), true), Times.Once);
            _mockDialogportenService.VerifyNoOtherCalls();
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

            _mockCorrespondenceRepository.Setup(x => x.CreateCorrespondence(It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceMockReturn);
            _mockCorrespondenceDeleteRepository.Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CorrespondenceDeleteEventEntity e, CancellationToken _) => e);
            _mockCorrespondenceDeleteRepository.Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
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
            _mockDialogportenService.Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync($"dialog-{correspondenceId}");

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);

            _mockCorrespondenceRepository.Verify(x => x.CreateCorrespondence(It.Is<CorrespondenceEntity>(c =>
                c.Altinn2CorrespondenceId == altinn2CorrespondenceId &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Published) &&
                c.Statuses.Any(s => s.Status == CorrespondenceStatus.Read)
            ), It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceRepository.Verify(x => x.AddExternalReference(
                 correspondenceId, ReferenceType.DialogportenDialogId, $"dialog-{correspondenceId}", It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceRepository.Verify(x => x.UpdateIsMigrating(
                correspondenceId, false, It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceRepository.VerifyNoOtherCalls();

            _mockCorrespondenceDeleteRepository.Verify(x => x.AddDeleteEvent(It.Is<CorrespondenceDeleteEventEntity>(e =>
                e.CorrespondenceId == correspondenceId &&
                e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient &&
                e.EventOccurred == request.DeleteEventEntities[0].EventOccurred &&
                e.SyncedFromAltinn2 != null
            ), It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceDeleteRepository.Verify(x => x.AddDeleteEvent(It.Is<CorrespondenceDeleteEventEntity>(e =>
                e.CorrespondenceId == correspondenceId &&
                e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient &&
                e.EventOccurred == request.DeleteEventEntities[1].EventOccurred &&
                e.SyncedFromAltinn2 != null
            ), It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceDeleteRepository.Verify(x => x.AddDeleteEvent(It.Is<CorrespondenceDeleteEventEntity>(e =>
                e.CorrespondenceId == correspondenceId &&
                e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient &&
                e.EventOccurred == request.DeleteEventEntities[2].EventOccurred &&
                e.SyncedFromAltinn2 != null
            ), It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceDeleteRepository.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _mockCorrespondenceDeleteRepository.VerifyNoOtherCalls();

            _mockDialogportenService.Verify(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                correspondenceId, It.IsAny<CorrespondenceEntity>(), It.IsAny<bool>(), true), Times.Once);
            _mockDialogportenService.VerifyNoOtherCalls();
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

        private static CorrespondenceEntity CreateMockMigratedCorrespondence(Guid correspondenceId, int altinn2CorrespondenceId)
        {
            return new CorrespondenceEntityBuilder()
                .WithResourceId("TTD-migratedCorrespondence-1-1")
                .WithAltinn2CorrespondenceId(altinn2CorrespondenceId)
                .WithId(correspondenceId)
                .WithStatus(CorrespondenceStatus.Published)
                .WithStatus(CorrespondenceStatus.Read)                
                .Build();
        }
    }
}
