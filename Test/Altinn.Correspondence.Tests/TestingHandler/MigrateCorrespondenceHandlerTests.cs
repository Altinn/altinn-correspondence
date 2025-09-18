using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.MigrateCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OneOf;
using System.Linq.Expressions;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class MigrateCorrespondenceHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _mockCorrespondenceRepository;
        private readonly Mock<IDialogportenService> _mockDialogportenService;
        private readonly Mock<IBackgroundJobClient> _mockBackgroundJobClient;
        private readonly Mock<ILogger<MigrateCorrespondenceHandler>> _mockLogger;
        private readonly Mock<ICorrespondenceDeleteEventRepository> _mockCorrespondenceDeleteRepository;
        private readonly MigrateCorrespondenceHandler _handler;

        public MigrateCorrespondenceHandlerTests()
        {
            _mockCorrespondenceRepository = new Mock<ICorrespondenceRepository>();
            _mockCorrespondenceDeleteRepository = new Mock<ICorrespondenceDeleteEventRepository>();
            _mockDialogportenService = new Mock<IDialogportenService>();
            _mockBackgroundJobClient = new Mock<IBackgroundJobClient>();            
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
                _mockLogger.Object);
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithBatchSizeExceedingLimit_ShouldScheduleRecursiveBackgroundJobs()
        {
            // Arrange
            var request = new MakeCorrespondenceAvailableRequest
            {
                BatchSize = 25000, // Exceeds the 10,000 limit
                AsyncProcessing = true,
                CreateEvents = false
            };

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0); // Should return MakeCorrespondenceAvailableResponse, not Error

            // Verify that background jobs were scheduled for the recursive batches
            // 25,000 should create 3 batches: 10,000 + 10,000 + 5,000
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "MakeCorrespondenceAvailable"),
                It.IsAny<IState>()), Times.Exactly(3));
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithBatchSizeExactlyAtLimit_ShouldCreateIndividualBackgroundJobs()
        {
            // Arrange
            var request = new MakeCorrespondenceAvailableRequest
            {
                BatchSize = 10000, // Exactly at the limit
                AsyncProcessing = true,
                CreateEvents = false
            };

            var mockCorrespondences = new List<CorrespondenceEntity>();
            for (int i = 0; i < 10000; i++)
            {
                mockCorrespondences.Add(CreateMockCorrespondence(Guid.NewGuid()));
            }

            _mockCorrespondenceRepository.Setup(x => x.GetCandidatesForMigrationToDialogporten(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockCorrespondences);

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);

            // Verify that individual background jobs were created for each correspondence
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "MakeCorrespondenceAvailableInDialogportenAndApi"),
                It.IsAny<IState>()), 
                Times.Exactly(10000));

            // Verify that no recursive batching jobs were created
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "MakeCorrespondenceAvailable"),
                It.IsAny<IState>()), 
                Times.Never);
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithLargeBatchSize_ShouldHandleRemainderCorrectly()
        {
            // Arrange
            var request = new MakeCorrespondenceAvailableRequest
            {
                BatchSize = 100001, // Just over 10 batches of 10,000 + 1 remainder
                AsyncProcessing = true,
                CreateEvents = true
            };

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);

            // Should create 11 batches: 10 batches of 10,000 + 1 batch of 1
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "MakeCorrespondenceAvailable"),
                It.IsAny<IState>()), Times.Exactly(11));
        }

        [Fact]
        public async Task MakeCorrespondenceAvailable_WithBatchSizeNotMultipleOfLimit_ShouldCalculateCorrectBatchSizes()
        {
            // Arrange
            var request = new MakeCorrespondenceAvailableRequest
            {
                BatchSize = 15000, // 1.5 batches
                AsyncProcessing = true,
                CreateEvents = false
            };

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);

            // Should create 2 batches: 10,000 + 5,000
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "MakeCorrespondenceAvailable"),
                It.IsAny<IState>()), Times.Exactly(2));
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
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
                request.BatchSize.Value, request.BatchOffset ?? 0, It.IsAny<CancellationToken>()), Times.Once);

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
        public async Task MakeCorrespondenceAvailable_WithBatchSizeBelowLimit_ShouldPassCorrectParametersToBackgroundJobs()
        {
            // Arrange
            var request = new MakeCorrespondenceAvailableRequest
            {
                BatchSize = 3,
                AsyncProcessing = true,
                CreateEvents = true
            };

            var correspondenceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var mockCorrespondences = correspondenceIds.Select(id => CreateMockCorrespondence(id)).ToList();

            _mockCorrespondenceRepository.Setup(x => x.GetCandidatesForMigrationToDialogporten(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockCorrespondences);

            // Act
            var result = await _handler.MakeCorrespondenceAvailable(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsT0);

            // Verify that background jobs were created with the correct correspondence IDs
            foreach (var correspondenceId in correspondenceIds)
            {
                _mockBackgroundJobClient.Verify(x => x.Create(
                    It.Is<Job>(job => 
                        job.Method.Name == "MakeCorrespondenceAvailableInDialogportenAndApi" &&
                        job.Args.Count > 0 &&
                        job.Args[0].Equals(correspondenceId)),
                    It.IsAny<IState>()), 
                    Times.Once);
            }
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
    }
}
