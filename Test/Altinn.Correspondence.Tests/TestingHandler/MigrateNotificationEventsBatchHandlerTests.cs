using Altinn.Correspondence.Application.MigrateNotificationEventsBatch;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
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
            x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
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

        var notifications = CreateNotificationBatch(batchCount, lastProcessed);

        _notificationRepositoryMock
            .Setup(x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert
        _notificationRepositoryMock.Verify(
            x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(job => 
                    job.Method.Name == nameof(IDialogportenService.AddNotificationActivity) &&
                    job.Type == typeof(IDialogportenService)),
                It.Is<IState>(state => state is EnqueuedState)),
            Times.Exactly(batchCount));
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
            .Setup(x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceNotificationEntity>());

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
    public async Task Process_WithValidBatch_EnqueuesJobsForEachNotification()
    {
        // Arrange
        var batchCount = 5;
        var lastProcessed = DateTimeOffset.UtcNow.AddDays(-1);

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        var notifications = CreateNotificationBatch(batchCount, lastProcessed);

        _notificationRepositoryMock
            .Setup(x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert
        foreach (var notification in notifications)
        {
            _backgroundJobClientMock.Verify(
                x => x.Create(
                    It.Is<Job>(job => 
                        job.Method.Name == nameof(IDialogportenService.AddNotificationActivity) &&
                        job.Type == typeof(IDialogportenService) &&
                        job.Args[0].Equals(notification.Id)),
                    It.Is<IState>(state => state is EnqueuedState)),
                Times.Once);
        }
    }

    [Fact]
    public async Task Process_WithValidBatch_EnqueuesNextBatchWithCorrectLastProcessedTime()
    {
        // Arrange
        var batchCount = 3;
        var lastProcessed = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        var notification1 = CreateNotificationWithSpecificDate(Guid.NewGuid(), new DateTimeOffset(2024, 1, 10, 12, 0, 0, TimeSpan.Zero));
        var notification2 = CreateNotificationWithSpecificDate(Guid.NewGuid(), new DateTimeOffset(2024, 1, 8, 12, 0, 0, TimeSpan.Zero));
        var notification3 = CreateNotificationWithSpecificDate(Guid.NewGuid(), new DateTimeOffset(2024, 1, 5, 12, 0, 0, TimeSpan.Zero));

        var notifications = new List<CorrespondenceNotificationEntity> { notification1, notification2, notification3 };

        _notificationRepositoryMock
            .Setup(x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert - Should use the minimum (oldest) date from the batch
        var expectedLastProcessed = new DateTimeOffset(2024, 1, 5, 12, 0, 0, TimeSpan.Zero);

        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(job => 
                    job.Method.Name == nameof(MigrateNotificationEventsBatchHandler.Process) &&
                    job.Type == typeof(MigrateNotificationEventsBatchHandler) &&
                    job.Args[0].Equals(batchCount) &&
                    job.Args[1].Equals(expectedLastProcessed)),
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

        // Repository returns empty list because notifications with null NotificationSent are filtered out
        _notificationRepositoryMock
            .Setup(x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceNotificationEntity>());

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
    public async Task Process_WithNotificationsHavingOnlyNotificationSent_UsesMinimumNotificationSentForNextBatch()
    {
        // Arrange
        var batchCount = 2;
        var lastProcessed = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        // Both notifications have NotificationSent values (as required by the new query logic)
        var notification1 = CreateNotificationWithRequestedTime(
            Guid.NewGuid(), 
            requestedSendTime: new DateTimeOffset(2024, 1, 10, 12, 0, 0, TimeSpan.Zero),
            notificationSent: new DateTimeOffset(2024, 1, 10, 14, 0, 0, TimeSpan.Zero));
        var notification2 = CreateNotificationWithRequestedTime(
            Guid.NewGuid(), 
            requestedSendTime: new DateTimeOffset(2024, 1, 5, 12, 0, 0, TimeSpan.Zero),
            notificationSent: new DateTimeOffset(2024, 1, 5, 15, 0, 0, TimeSpan.Zero));

        var notifications = new List<CorrespondenceNotificationEntity> { notification1, notification2 };

        _notificationRepositoryMock
            .Setup(x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert - Should use the minimum NotificationSent (with fallback to RequestedSendTime if null)
        var expectedLastProcessed = new DateTimeOffset(2024, 1, 5, 15, 0, 0, TimeSpan.Zero);

        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(job => 
                    job.Method.Name == nameof(MigrateNotificationEventsBatchHandler.Process) &&
                    job.Args[1].Equals(expectedLastProcessed)),
                It.Is<IState>(state => state is EnqueuedState)),
            Times.Once);
    }

    [Fact]
    public async Task Process_WithMultipleNotificationSent_UsesCorrectMinimumDate()
    {
        // Arrange
        var batchCount = 3;
        var lastProcessed = new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero);

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        // All notifications have NotificationSent (repository filters out null values)
        var notification1 = CreateNotificationWithRequestedTime(
            Guid.NewGuid(),
            requestedSendTime: new DateTimeOffset(2024, 1, 10, 12, 0, 0, TimeSpan.Zero),
            notificationSent: new DateTimeOffset(2024, 1, 10, 14, 0, 0, TimeSpan.Zero));
        var notification2 = CreateNotificationWithRequestedTime(
            Guid.NewGuid(),
            requestedSendTime: new DateTimeOffset(2024, 1, 8, 12, 0, 0, TimeSpan.Zero),
            notificationSent: new DateTimeOffset(2024, 1, 8, 13, 0, 0, TimeSpan.Zero));
        var notification3 = CreateNotificationWithRequestedTime(
            Guid.NewGuid(),
            requestedSendTime: new DateTimeOffset(2024, 1, 5, 12, 0, 0, TimeSpan.Zero),
            notificationSent: new DateTimeOffset(2024, 1, 5, 15, 0, 0, TimeSpan.Zero));

        var notifications = new List<CorrespondenceNotificationEntity> { notification1, notification2, notification3 };

        _notificationRepositoryMock
            .Setup(x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert - Should use the minimum NotificationSent: notification3 (2024-01-05 15:00)
        var expectedLastProcessed = new DateTimeOffset(2024, 1, 5, 15, 0, 0, TimeSpan.Zero);

        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(job => 
                    job.Method.Name == nameof(MigrateNotificationEventsBatchHandler.Process) &&
                    job.Args[1].Equals(expectedLastProcessed)),
                It.Is<IState>(state => state is EnqueuedState)),
            Times.Once);
    }

    [Fact]
    public async Task Process_LogsProcessingInformation()
    {
        // Arrange
        var batchCount = 3;
        var lastProcessed = DateTimeOffset.UtcNow.AddDays(-1);

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(0);

        var notifications = CreateNotificationBatch(batchCount, lastProcessed);

        _notificationRepositoryMock
            .Setup(x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => 
                    o.ToString()!.Contains("Processing") && 
                    o.ToString()!.Contains(batchCount.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_WhenQueueHasFewerThan5Batches_ProcessesBatch()
    {
        // Arrange
        var batchCount = 100;
        var lastProcessed = DateTimeOffset.UtcNow.AddDays(-1);
        var enqueuedJobCount = 300; // Less than 5 batches worth

        _monitoringApiMock
            .Setup(x => x.EnqueuedCount(HangfireQueues.Migration))
            .Returns(enqueuedJobCount);

        var notifications = CreateNotificationBatch(batchCount, lastProcessed);

        _notificationRepositoryMock
            .Setup(x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        // Act
        await _handler.Process(batchCount, lastProcessed);

        // Assert
        _notificationRepositoryMock.Verify(
            x => x.GetSyncedNotificationsWithoutDialogActivityBatch(
                batchCount,
                lastProcessed,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should not schedule, should process immediately
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.IsAny<Job>(),
                It.Is<IState>(state => state is ScheduledState)),
            Times.Never);
    }

    // Helper methods
    private List<CorrespondenceNotificationEntity> CreateNotificationBatch(int count, DateTimeOffset baseDate)
    {
        var notifications = new List<CorrespondenceNotificationEntity>();
        for (int i = 0; i < count; i++)
        {
            notifications.Add(CreateNotificationWithSpecificDate(Guid.NewGuid(), baseDate.AddHours(-i)));
        }
        return notifications;
    }

    private CorrespondenceNotificationEntity CreateNotificationWithSpecificDate(
        Guid id, 
        DateTimeOffset notificationSent)
    {
        return new CorrespondenceNotificationEntity
        {
            Id = id,
            CorrespondenceId = Guid.NewGuid(),
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = notificationSent.AddMinutes(-30),
            Created = DateTimeOffset.UtcNow.AddDays(-2),
            IsReminder = false,
            NotificationSent = notificationSent,
            SyncedFromAltinn2 = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    private CorrespondenceNotificationEntity CreateNotificationWithRequestedTime(
        Guid id,
        DateTimeOffset requestedSendTime,
        DateTimeOffset? notificationSent)
    {
        return new CorrespondenceNotificationEntity
        {
            Id = id,
            CorrespondenceId = Guid.NewGuid(),
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = requestedSendTime,
            Created = DateTimeOffset.UtcNow.AddDays(-2),
            IsReminder = false,
            NotificationSent = notificationSent,
            SyncedFromAltinn2 = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }
}
