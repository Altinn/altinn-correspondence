using Altinn.Correspondence.Application.MigrateNotificationEventsBatch;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class MigrateNotificationEventsBatchHandlerTests
{
    private readonly Mock<ICorrespondenceNotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly Mock<ILogger<MigrateNotificationEventsBatchHandler>> _loggerMock;
    private readonly Mock<JobStorage> _jobStorageMock;
    private readonly Mock<IMonitoringApi> _monitoringApiMock;
    private readonly MigrateNotificationEventsBatchHandler _handler;

    public MigrateNotificationEventsBatchHandlerTests()
    {
        _notificationRepositoryMock = new Mock<ICorrespondenceNotificationRepository>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _loggerMock = new Mock<ILogger<MigrateNotificationEventsBatchHandler>>();
        _jobStorageMock = new Mock<JobStorage>();
        _monitoringApiMock = new Mock<IMonitoringApi>();

        _jobStorageMock.Setup(x => x.GetMonitoringApi()).Returns(_monitoringApiMock.Object);
        JobStorage.Current = _jobStorageMock.Object;

        _handler = new MigrateNotificationEventsBatchHandler(
            _notificationRepositoryMock.Object,
            _backgroundJobClientMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Process_WhenQueueHasTooManyJobs_SchedulesForLater()
    {
        // Arrange
        var batchCount = 100;
        var lastProcessed = DateTimeOffset.UtcNow.AddDays(-1);
        var enqueuedJobCount = 600; // More than 5 batches worth (5 * 100 = 500)

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(enqueuedJobCount);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(job => 
                    job.Method.Name == nameof(MigrateNotificationEventsBatchHandler.Process) &&
                    job.Type == typeof(MigrateNotificationEventsBatchHandler)),
                It.Is<IState>(state => state is ScheduledState)),
            Times.Once);

        _notificationRepositoryMock.Verify(
            x => x.GetCorrespondencesWithSyncedNotifications(
                It.IsAny<int>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenQueueHasExactly5BatchesWorth_ProcessesBatch()
    {
        // Arrange
        var batchCount = 100;
        var lastProcessed = DateTimeOffset.UtcNow.AddDays(-1);
        var enqueuedJobCount = 500; // Exactly 5 batches worth

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(enqueuedJobCount);

        var batch = CreateCorrespondenceBatch(5, batchCount, lastProcessed); // 5 correspondences with total 100 notifications

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert
        _notificationRepositoryMock.Verify(
            x => x.GetCorrespondencesWithSyncedNotifications(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should enqueue one job per correspondence (not per notification)
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(job => 
                    job.Method.Name == nameof(IDialogportenService.AddNotificationActivitiesWithDuplicateCheck) &&
                    job.Type == typeof(IDialogportenService)),
                It.Is<IState>(state => state is EnqueuedState)),
            Times.Exactly(batch.Correspondences.Count));
    }

    [Fact]
    public async Task Process_WhenBatchIsEmpty_LogsCompletionAndDoesNotEnqueueMoreJobs()
    {
        // Arrange
        var batchCount = 100;
        var lastProcessed = DateTimeOffset.UtcNow.AddDays(-1);

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CorrespondencesWithNotificationsBatch());

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("No more notification events to process")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.IsAny<Job>(),
                It.IsAny<IState>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WithValidBatch_EnqueuesJobsForEachCorrespondence()
    {
        // Arrange
        var batchCount = 50;
        var lastProcessed = DateTimeOffset.UtcNow.AddDays(-1);

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        var batch = CreateCorrespondenceBatch(5, batchCount, lastProcessed); // 5 correspondences

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert
        foreach (var correspondenceGroup in batch.Correspondences)
        {
            _backgroundJobClientMock.Verify(
                x => x.Create(
                    It.Is<Job>(job => 
                        job.Method.Name == nameof(IDialogportenService.AddNotificationActivitiesWithDuplicateCheck) &&
                        job.Type == typeof(IDialogportenService) &&
                        job.Args[0].Equals(correspondenceGroup.CorrespondenceId) &&
                        ((List<Guid>)job.Args[1]).Count == correspondenceGroup.NotificationIds.Count),
                    It.Is<IState>(state => state is EnqueuedState)),
                Times.Once);
        }
    }

    [Fact]
    public async Task Process_WithValidBatch_EnqueuesNextBatchWithCorrectCursor()
    {
        // Arrange
        var batchCount = 50;
        var lastProcessed = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var oldestTimestamp = new DateTimeOffset(2024, 1, 5, 12, 0, 0, TimeSpan.Zero);
        var oldestId = Guid.NewGuid();

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        var batch = new CorrespondencesWithNotificationsBatch
        {
            Correspondences = new List<CorrespondenceWithNotifications>
            {
                new CorrespondenceWithNotifications
                {
                    CorrespondenceId = Guid.NewGuid(),
                    NotificationIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
                }
            },
            OldestNotificationTimestamp = oldestTimestamp,
            OldestNotificationId = oldestId,
            TotalNotificationCount = 2
        };

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                batchCount,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert - Should use cursor information from batch
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(job => 
                    job.Method.Name == nameof(MigrateNotificationEventsBatchHandler.Process) &&
                    job.Type == typeof(MigrateNotificationEventsBatchHandler) &&
                    job.Args[0].Equals(batchCount) &&
                    job.Args[1].Equals(oldestTimestamp) &&
                    job.Args[2].Equals(oldestId)),
                It.Is<IState>(state => state is EnqueuedState)),
            Times.Once);
    }

    [Fact]
    public async Task Process_WhenRepositoryReturnsEmptyBatchDueToFiltering_CompletesGracefully()
    {
        // Arrange
        var batchCount = 100;
        var lastProcessed = DateTimeOffset.UtcNow.AddDays(-1);

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        // Repository returns empty batch
        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CorrespondencesWithNotificationsBatch());

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert - Should log completion and not enqueue any more jobs
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("No more notification events to process")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.IsAny<Job>(),
                It.IsAny<IState>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_GroupsNotificationsByCorrespondence_LogsGroupingStats()
    {
        // Arrange
        var batchCount = 100;
        var lastProcessed = DateTimeOffset.UtcNow.AddDays(-1);

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        // 3 correspondences with different numbers of notifications
        var batch = new CorrespondencesWithNotificationsBatch
        {
            Correspondences = new List<CorrespondenceWithNotifications>
            {
                new CorrespondenceWithNotifications
                {
                    CorrespondenceId = Guid.NewGuid(),
                    NotificationIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }
                },
                new CorrespondenceWithNotifications
                {
                    CorrespondenceId = Guid.NewGuid(),
                    NotificationIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
                },
                new CorrespondenceWithNotifications
                {
                    CorrespondenceId = Guid.NewGuid(),
                    NotificationIds = new List<Guid> { Guid.NewGuid() }
                }
            },
            OldestNotificationTimestamp = DateTimeOffset.UtcNow.AddDays(-2),
            OldestNotificationId = Guid.NewGuid(),
            TotalNotificationCount = 6
        };

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert - Should log information about grouping
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => 
                    o.ToString()!.Contains("3 correspondences") && 
                    o.ToString()!.Contains("6 total notification events")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Should enqueue exactly 3 jobs (one per correspondence)
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(job => 
                    job.Method.Name == nameof(IDialogportenService.AddNotificationActivitiesWithDuplicateCheck)),
                It.Is<IState>(state => state is EnqueuedState)),
            Times.Exactly(3));
    }

    // Helper methods
    private CorrespondencesWithNotificationsBatch CreateCorrespondenceBatch(int correspondenceCount, int totalNotifications, DateTimeOffset baseDate)
    {
        var correspondences = new List<CorrespondenceWithNotifications>();
        var notificationsPerCorrespondence = totalNotifications / correspondenceCount;
        var oldestTimestamp = baseDate;
        Guid? oldestId = null;

        for (int i = 0; i < correspondenceCount; i++)
        {
            var notificationIds = new List<Guid>();
            for (int j = 0; j < notificationsPerCorrespondence; j++)
            {
                var notifId = Guid.NewGuid();
                notificationIds.Add(notifId);

                if (i == correspondenceCount - 1 && j == notificationsPerCorrespondence - 1)
                {
                    oldestId = notifId;
                }
            }

            correspondences.Add(new CorrespondenceWithNotifications
            {
                CorrespondenceId = Guid.NewGuid(),
                NotificationIds = notificationIds
            });
        }

        return new CorrespondencesWithNotificationsBatch
        {
            Correspondences = correspondences,
            OldestNotificationTimestamp = oldestTimestamp.AddHours(-(correspondenceCount * notificationsPerCorrespondence)),
            OldestNotificationId = oldestId,
            TotalNotificationCount = totalNotifications
        };
    }
}
