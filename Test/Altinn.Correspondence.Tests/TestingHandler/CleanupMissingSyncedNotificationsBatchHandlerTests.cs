using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Application.CleanupMissingSyncedNotificationsBatch;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CleanupMissingSyncedNotificationsBatchHandlerTests
{
    private readonly Mock<ICorrespondenceNotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly Mock<ChainedBatchJobOrchestrator> _orchestratorMock;
    private readonly Mock<ILogger<CleanupMissingSyncedNotificationsBatchJob>> _batchJobLoggerMock;
    private readonly Mock<ILogger<CleanupMissingSyncedNotificationsBatchHandler>> _handlerLoggerMock;

    private readonly CleanupMissingSyncedNotificationsBatchJob _batchJob;
    private readonly CleanupMissingSyncedNotificationsBatchHandler _handler;

    public CleanupMissingSyncedNotificationsBatchHandlerTests()
    {
        _notificationRepositoryMock = new Mock<ICorrespondenceNotificationRepository>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _orchestratorMock = new Mock<ChainedBatchJobOrchestrator>(Mock.Of<ILogger<ChainedBatchJobOrchestrator>>());
        _batchJobLoggerMock = new Mock<ILogger<CleanupMissingSyncedNotificationsBatchJob>>();
        _handlerLoggerMock = new Mock<ILogger<CleanupMissingSyncedNotificationsBatchHandler>>();

        _batchJob = new CleanupMissingSyncedNotificationsBatchJob(
            _notificationRepositoryMock.Object,
            _backgroundJobClientMock.Object,
            _batchJobLoggerMock.Object);

        _handler = new CleanupMissingSyncedNotificationsBatchHandler(
            _backgroundJobClientMock.Object,
            _orchestratorMock.Object,
            _batchJob,
            _handlerLoggerMock.Object);
    }

    [Fact]
    public async Task Process_EnqueuesOrchestratorJob()
    {
        // Arrange
        var batchCount = 100;
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        var id = Guid.NewGuid();

        _backgroundJobClientMock
            .Setup(x => x.Create(
                It.IsAny<Hangfire.Common.Job>(),
                It.IsAny<Hangfire.States.IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, timestamp, id);

        // Assert - Should enqueue orchestrator job on live-migration queue
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Hangfire.Common.Job>(job => 
                    job.Type == typeof(CleanupMissingSyncedNotificationsBatchHandler) &&
                    job.Method.Name == nameof(CleanupMissingSyncedNotificationsBatchHandler.ExecuteBatch)),
                It.Is<Hangfire.States.IState>(state => 
                    state is Hangfire.States.EnqueuedState)),
            Times.Once);
    }

    [Fact]
    public async Task BatchJob_FetchesAndProcessesBatch()
    {
        // Arrange
        var request = new CleanupMissingSyncedNotificationsBatchRequest
        {
            BatchSize = 100,
            CursorNotificationSent = DateTimeOffset.UtcNow,
            CursorId = Guid.NewGuid()
        };

        var batch = CreateTestBatch(3, 10);

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                request.BatchSize,
                request.CursorNotificationSent.Value,
                request.CursorId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        var definition = _batchJob.CreateDefinition();

        // Act
        var fetchResult = await definition.FetchBatchAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(3, fetchResult.Items.Count);
        Assert.False(fetchResult.HasMoreBatches); // 10 total notifications < 100 batch size
    }

    [Fact]
    public async Task BatchJob_ProcessBatchAsync_EnqueuesWorkerJobs()
    {
        // Arrange
        var request = new CleanupMissingSyncedNotificationsBatchRequest
        {
            BatchSize = 100,
            TotalCorrespondencesProcessed = 0,
            TotalNotificationsProcessed = 0
        };

        var batch = CreateTestBatch(3, 12);
        var items = batch.Correspondences;

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                It.IsAny<int>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        _backgroundJobClientMock
            .Setup(x => x.Create(
                It.IsAny<Hangfire.Common.Job>(),
                It.IsAny<Hangfire.States.IState>()))
            .Returns("job-id");

        var definition = _batchJob.CreateDefinition();

        // Act
        var updatedState = await definition.ProcessBatchAsync!(request, items, CancellationToken.None);

        // Assert - Should enqueue one worker job per correspondence on migration queue
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Hangfire.Common.Job>(job => 
                    job.Type == typeof(IDialogportenService) &&
                    job.Method.Name == nameof(IDialogportenService.AddNotificationActivitiesWithDuplicateCheck)),
                It.Is<Hangfire.States.IState>(state => 
                    state is Hangfire.States.EnqueuedState)),
            Times.Exactly(3));

        // Verify state was updated with counters
        Assert.Equal(3, updatedState.TotalCorrespondencesProcessed);
        Assert.Equal(12, updatedState.TotalNotificationsProcessed);
        Assert.Equal(batch.OldestNotificationTimestamp, updatedState.CursorNotificationSent);
        Assert.Equal(batch.OldestNotificationId, updatedState.CursorId);
    }

    [Fact]
    public async Task BatchJob_HasMoreBatches_WhenFullBatchReturned()
    {
        // Arrange
        var request = new CleanupMissingSyncedNotificationsBatchRequest
        {
            BatchSize = 100
        };

        // Create a batch with exactly BatchSize notifications
        var batch = CreateTestBatch(10, 100);

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                request.BatchSize,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        var definition = _batchJob.CreateDefinition();

        // Act
        var fetchResult = await definition.FetchBatchAsync(request, CancellationToken.None);

        // Assert
        Assert.True(fetchResult.HasMoreBatches);
    }

    [Fact]
    public async Task BatchJob_NoMoreBatches_WhenPartialBatchReturned()
    {
        // Arrange
        var request = new CleanupMissingSyncedNotificationsBatchRequest
        {
            BatchSize = 100
        };

        // Create a batch with fewer than BatchSize notifications
        var batch = CreateTestBatch(5, 50);

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                request.BatchSize,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        var definition = _batchJob.CreateDefinition();

        // Act
        var fetchResult = await definition.FetchBatchAsync(request, CancellationToken.None);

        // Assert
        Assert.False(fetchResult.HasMoreBatches);
    }

    [Fact]
    public void BatchJob_OnComplete_LogsFinalStatistics()
    {
        // Arrange
        var definition = _batchJob.CreateDefinition();
        var finalState = new CleanupMissingSyncedNotificationsBatchRequest
        {
            TotalCorrespondencesProcessed = 150,
            TotalNotificationsProcessed = 1500
        };

        // Act
        definition.OnComplete?.Invoke(finalState);

        // Assert - Verify completion was logged
        _batchJobLoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) =>
                    o.ToString()!.Contains("complete") &&
                    o.ToString()!.Contains("150") &&
                    o.ToString()!.Contains("1500")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task BatchJob_ProcessBatchAsync_HandlesCorrespondenceWithMixedNotificationCounts()
    {
        // Arrange
        var request = new CleanupMissingSyncedNotificationsBatchRequest
        {
            BatchSize = 100,
            TotalCorrespondencesProcessed = 0,
            TotalNotificationsProcessed = 0
        };

        // Create a realistic batch with varying notification counts per correspondence
        // This simulates what happens when some correspondences have many notifications
        // and the batch job needs to handle them all
        var batch = CreateTestBatch(5, 50); // 5 correspondences with 50 total notifications

        // Override to create a more realistic distribution
        batch.Correspondences = new List<CorrespondenceWithNotifications>
        {
            new() { CorrespondenceId = Guid.NewGuid(), NotificationIds = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList() },
            new() { CorrespondenceId = Guid.NewGuid(), NotificationIds = Enumerable.Range(0, 15).Select(_ => Guid.NewGuid()).ToList() },
            new() { CorrespondenceId = Guid.NewGuid(), NotificationIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList() },
            new() { CorrespondenceId = Guid.NewGuid(), NotificationIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList() },
            new() { CorrespondenceId = Guid.NewGuid(), NotificationIds = Enumerable.Range(0, 2).Select(_ => Guid.NewGuid()).ToList() }
        };
        batch.TotalNotificationCount = 50;
        var items = batch.Correspondences;

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                It.IsAny<int>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        _backgroundJobClientMock
            .Setup(x => x.Create(
                It.IsAny<Hangfire.Common.Job>(),
                It.IsAny<Hangfire.States.IState>()))
            .Returns("job-id");

        var definition = _batchJob.CreateDefinition();

        // Act
        var updatedState = await definition.ProcessBatchAsync!(request, items, CancellationToken.None);

        // Assert - Should enqueue one worker job per correspondence regardless of notification count
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Hangfire.Common.Job>(job => 
                    job.Type == typeof(IDialogportenService) &&
                    job.Method.Name == nameof(IDialogportenService.AddNotificationActivitiesWithDuplicateCheck)),
                It.Is<Hangfire.States.IState>(state => 
                    state is Hangfire.States.EnqueuedState)),
            Times.Exactly(5));

        // Verify each correspondence gets its correct notification IDs passed
        foreach (var correspondence in batch.Correspondences)
        {
            _backgroundJobClientMock.Verify(
                x => x.Create(
                    It.Is<Hangfire.Common.Job>(job => 
                        job.Type == typeof(IDialogportenService) &&
                        job.Method.Name == nameof(IDialogportenService.AddNotificationActivitiesWithDuplicateCheck) &&
                        job.Args[0].Equals(correspondence.CorrespondenceId) &&
                        ((List<Guid>)job.Args[1]).Count == correspondence.NotificationIds.Count),
                    It.IsAny<Hangfire.States.IState>()),
                Times.Once,
                $"Expected job with {correspondence.NotificationIds.Count} notifications for correspondence {correspondence.CorrespondenceId}");
        }

        // Verify state counters
        Assert.Equal(5, updatedState.TotalCorrespondencesProcessed);
        Assert.Equal(50, updatedState.TotalNotificationsProcessed);
    }

    [Fact]
    public async Task BatchJob_ProcessBatchAsync_HandlesEmptyNotificationList()
    {
        // Arrange - Edge case: correspondence exists but has zero synced notifications
        var request = new CleanupMissingSyncedNotificationsBatchRequest
        {
            BatchSize = 100,
            TotalCorrespondencesProcessed = 0,
            TotalNotificationsProcessed = 0
        };

        var batch = new CorrespondencesWithNotificationsBatch
        {
            Correspondences = new List<CorrespondenceWithNotifications>
            {
                new() { CorrespondenceId = Guid.NewGuid(), NotificationIds = new List<Guid>() }
            },
            OldestNotificationTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            OldestNotificationId = Guid.NewGuid(),
            TotalNotificationCount = 0
        };
        var items = batch.Correspondences;

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                It.IsAny<int>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        _backgroundJobClientMock
            .Setup(x => x.Create(
                It.IsAny<Hangfire.Common.Job>(),
                It.IsAny<Hangfire.States.IState>()))
            .Returns("job-id");

        var definition = _batchJob.CreateDefinition();

        // Act
        var updatedState = await definition.ProcessBatchAsync!(request, items, CancellationToken.None);

        // Assert - Should still enqueue job even with empty notification list
        // The AddNotificationActivitiesWithDuplicateCheck method handles empty lists gracefully
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Hangfire.Common.Job>(job => 
                    job.Type == typeof(IDialogportenService) &&
                    job.Method.Name == nameof(IDialogportenService.AddNotificationActivitiesWithDuplicateCheck)),
                It.IsAny<Hangfire.States.IState>()),
            Times.Once);

        // Verify state was updated
        Assert.Equal(1, updatedState.TotalCorrespondencesProcessed);
        Assert.Equal(0, updatedState.TotalNotificationsProcessed);
    }

    // Helper method to create test batch data
    private CorrespondencesWithNotificationsBatch CreateTestBatch(int correspondenceCount, int totalNotifications)
    {
        var correspondences = new List<CorrespondenceWithNotifications>();
        var notificationsPerCorrespondence = totalNotifications / correspondenceCount;
        var remainder = totalNotifications % correspondenceCount;

        for (int i = 0; i < correspondenceCount; i++)
        {
            // Distribute remainder across first N correspondences
            var notificationCount = notificationsPerCorrespondence + (i < remainder ? 1 : 0);
            var notificationIds = Enumerable.Range(0, notificationCount)
                .Select(_ => Guid.NewGuid())
                .ToList();

            correspondences.Add(new CorrespondenceWithNotifications
            {
                CorrespondenceId = Guid.NewGuid(),
                NotificationIds = notificationIds
            });
        }

        return new CorrespondencesWithNotificationsBatch
        {
            Correspondences = correspondences,
            OldestNotificationTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            OldestNotificationId = Guid.NewGuid(),
            TotalNotificationCount = totalNotifications
        };
    }

    [Fact]
    public async Task ExecuteBatch_EnqueuesDialogportenJobsForEachCorrespondence()
    {
        // Arrange - Create test batch with multiple correspondences
        var correspondence1Id = Guid.NewGuid();
        var correspondence2Id = Guid.NewGuid();
        var notification1 = Guid.NewGuid();
        var notification2 = Guid.NewGuid();
        var notification3 = Guid.NewGuid();

        var batch = new CorrespondencesWithNotificationsBatch
        {
            Correspondences = new List<CorrespondenceWithNotifications>
            {
                new() { CorrespondenceId = correspondence1Id, NotificationIds = new List<Guid> { notification1, notification2 } },
                new() { CorrespondenceId = correspondence2Id, NotificationIds = new List<Guid> { notification3 } }
            },
            OldestNotificationTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            OldestNotificationId = notification3,
            TotalNotificationCount = 3
        };

        _notificationRepositoryMock
            .Setup(x => x.GetCorrespondencesWithSyncedNotifications(
                It.IsAny<int>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        var enqueuedJobs = new List<(Type serviceType, string methodName, Guid correspondenceId, List<Guid> notificationIds)>();

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
            .Returns<Hangfire.Common.Job, Hangfire.States.IState>((job, state) =>
            {
                if (job.Type == typeof(IDialogportenService) && 
                    job.Method.Name == nameof(IDialogportenService.AddNotificationActivitiesWithDuplicateCheck))
                {
                    var correspondenceId = (Guid)job.Args[0];
                    var notificationIds = (List<Guid>)job.Args[1];
                    enqueuedJobs.Add((job.Type, job.Method.Name, correspondenceId, notificationIds));
                }
                return Guid.NewGuid().ToString();
            });

        var request = new CleanupMissingSyncedNotificationsBatchRequest
        {
            BatchSize = 50,
            CursorNotificationSent = DateTimeOffset.MaxValue,
            CursorId = null
        };

        var definition = _batchJob.CreateDefinition();

        // Act - Process the batch
        await definition.ProcessBatchAsync!(request, batch.Correspondences, CancellationToken.None);

        // Assert - Verify jobs were enqueued for each correspondence with correct notification IDs
        Assert.Equal(2, enqueuedJobs.Count);

        var job1 = enqueuedJobs.FirstOrDefault(j => j.correspondenceId == correspondence1Id);
        Assert.NotEqual(default, job1);
        Assert.Equal(2, job1.notificationIds.Count);
        Assert.Contains(notification1, job1.notificationIds);
        Assert.Contains(notification2, job1.notificationIds);

        var job2 = enqueuedJobs.FirstOrDefault(j => j.correspondenceId == correspondence2Id);
        Assert.NotEqual(default, job2);
        Assert.Single(job2.notificationIds);
        Assert.Contains(notification3, job2.notificationIds);
    }
}
