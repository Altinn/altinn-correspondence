using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.CleanupMissingSyncedNotificationsBatch;

public class CleanupMissingSyncedNotificationsBatchJob(
    ICorrespondenceNotificationRepository notificationRepository,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CleanupMissingSyncedNotificationsBatchJob> logger)
{
    public const int DefaultBatchSize = 1000;

    public ChainedBatchJobDefinition<CleanupMissingSyncedNotificationsBatchRequest, CorrespondenceWithNotifications> CreateDefinition() =>
        new()
        {
            Settings = new ChainedBatchJobSettings
            {
                JobName = "CleanupMissingSyncedNotifications",
                BatchSize = DefaultBatchSize,
                BackpressureLimit = DefaultBatchSize * 5,
            },
            FetchBatchAsync = async (request, cancellationToken) =>
            {
                var batch = await notificationRepository.GetCorrespondencesWithSyncedNotifications(
                    request.BatchSize,
                    request.CursorNotificationSent ?? DateTimeOffset.MaxValue,
                    request.CursorId,
                    cancellationToken);

                // HasMoreBatches is true if we got a full batch (indicating more data may exist)
                var hasMoreBatches = batch.TotalNotificationCount == request.BatchSize;

                return new ChainedBatchJobFetchResult<CorrespondenceWithNotifications>(
                    batch.Correspondences,
                    hasMoreBatches);
            },
            GetCursorFromItem = correspondence =>
            {
                // This won't be called because we use ProcessBatchAsync which updates state directly
                // The framework requires this but we handle cursor in ProcessBatchAsync where we have access to batch metadata
                return new KeysetCursor(DateTimeOffset.MinValue, correspondence.CorrespondenceId);
            },
            ProcessBatchAsync = async (request, items, cancellationToken) =>
            {
                // Re-fetch to get the full batch metadata including cursor information
                var batch = await notificationRepository.GetCorrespondencesWithSyncedNotifications(
                    request.BatchSize,
                    request.CursorNotificationSent ?? DateTimeOffset.MaxValue,
                    request.CursorId,
                    cancellationToken);

                foreach (var correspondenceGroup in batch.Correspondences)
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(
                        ChainedBatchJobQueues.Worker,
                        service => service.AddNotificationActivitiesWithDuplicateCheck(
                            correspondenceGroup.CorrespondenceId,
                            correspondenceGroup.NotificationIds,
                            CancellationToken.None));
                }

                logger.LogInformation(
                    "Enqueued {CorrespondenceCount} worker jobs for correspondences with {NotificationCount} notifications. " +
                    "Next cursor: {NextTimestamp} / {NextId}",
                    batch.Correspondences.Count,
                    batch.TotalNotificationCount,
                    batch.OldestNotificationTimestamp,
                    batch.OldestNotificationId);

                // Return updated state with new cursor and counters
                return request with
                {
                    CursorNotificationSent = batch.OldestNotificationTimestamp,
                    CursorId = batch.OldestNotificationId,
                    TotalCorrespondencesProcessed = request.TotalCorrespondencesProcessed + batch.Correspondences.Count,
                    TotalNotificationsProcessed = request.TotalNotificationsProcessed + batch.TotalNotificationCount
                };
            },
            CreateNextState = (request, cursor, fetchedCount) =>
            {
                // State is already fully updated in ProcessBatchAsync
                // This is called by the framework but we just return the request as-is
                return request;
            },
            EnqueueNextBatch = nextState =>
                backgroundJobClient.Enqueue<CleanupMissingSyncedNotificationsBatchHandler>(
                    ChainedBatchJobQueues.Orchestrator,
                    handler => handler.ExecuteBatch(nextState, CancellationToken.None)),
            RescheduleBatch = state =>
                backgroundJobClient.Schedule<CleanupMissingSyncedNotificationsBatchHandler>(
                    ChainedBatchJobQueues.Orchestrator,
                    handler => handler.ExecuteBatch(state, CancellationToken.None),
                    TimeSpan.FromMinutes(1)),
            OnComplete = state =>
                logger.LogInformation(
                    "Notification event migration complete. Processed {TotalCorrespondences} correspondences with {TotalNotifications} notifications",
                    state.TotalCorrespondencesProcessed,
                    state.TotalNotificationsProcessed)
        };
}
