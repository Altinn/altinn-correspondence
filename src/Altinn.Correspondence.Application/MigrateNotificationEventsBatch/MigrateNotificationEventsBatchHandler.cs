using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.MigrateNotificationEventsBatch
{
    public class MigrateNotificationEventsBatchHandler(
        ICorrespondenceNotificationRepository notificationRepository,
        IBackgroundJobClient backgroundJobClient,
        ILogger<MigrateNotificationEventsBatchHandler> logger)
    {
        public async Task Process(int batchCount, DateTimeOffset lastProcessedTimestamp, Guid? lastProcessedId = null)
        {
            var enqueuedJobs = JobStorage.Current.GetMonitoringApi().EnqueuedCount(HangfireQueues.Migration);
            if (enqueuedJobs > batchCount * 5)
            {
                // If there are more than 5 batches worth of jobs already enqueued, we should wait before enqueuing more to avoid overwhelming the system
                logger.LogWarning(
                    "Migration queue has {EnqueuedJobs} jobs (threshold: {Threshold}). " +
                    "Delaying next batch by 1 minute to prevent queue overflow. " +
                    "Current processing threshold: {LastProcessedTimestamp} / {LastProcessedId}",
                    enqueuedJobs,
                    batchCount * 5,
                    lastProcessedTimestamp,
                    lastProcessedId);

                backgroundJobClient.Schedule<MigrateNotificationEventsBatchHandler>(
                    HangfireQueues.Migration, 
                    handler => handler.Process(batchCount, lastProcessedTimestamp, lastProcessedId), 
                    DateTime.UtcNow.AddMinutes(1));
            }
            else
            {
                var batch = await notificationRepository.GetCorrespondencesWithSyncedNotifications(
                    batchCount, 
                    lastProcessedTimestamp,
                    lastProcessedId,
                    CancellationToken.None);

                if (batch.Correspondences.Count == 0)
                {
                    logger.LogInformation("No more notification events to process. Migration of notification events is complete.");
                    return; // No more events to process
                }

                logger.LogInformation(
                    "Processing {CorrespondenceCount} correspondences with {NotificationCount} total notification events. " +
                    "Next batch will process notifications older than {NextThreshold} (Id: {NextId})",
                    batch.Correspondences.Count,
                    batch.TotalNotificationCount,
                    batch.OldestNotificationTimestamp,
                    batch.OldestNotificationId);

                foreach (var correspondenceGroup in batch.Correspondences)
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(
                        HangfireQueues.Migration, 
                        service => service.AddNotificationActivitiesWithDuplicateCheck(
                            correspondenceGroup.CorrespondenceId, 
                            correspondenceGroup.NotificationIds, 
                            CancellationToken.None));
                }

                // Enqueue next batch with composite cursor to ensure no notifications are skipped at timestamp boundaries
                backgroundJobClient.Enqueue<MigrateNotificationEventsBatchHandler>(
                    HangfireQueues.Migration, 
                    handler => handler.Process(batchCount, batch.OldestNotificationTimestamp!.Value, batch.OldestNotificationId));
            }
        }
    }
}
