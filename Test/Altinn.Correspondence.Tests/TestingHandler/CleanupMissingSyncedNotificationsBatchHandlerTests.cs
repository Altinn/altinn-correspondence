using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Application.CleanupMissingSyncedNotificationsBatch;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Hangfire;
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

    // Helper method to create test batch data
    private CorrespondencesWithNotificationsBatch CreateTestBatch(int correspondenceCount, int totalNotifications)
    {
        var correspondences = new List<CorrespondenceWithNotifications>();
        var notificationsPerCorrespondence = totalNotifications / correspondenceCount;

        for (int i = 0; i < correspondenceCount; i++)
        {
            var notificationIds = Enumerable.Range(0, notificationsPerCorrespondence)
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
}
