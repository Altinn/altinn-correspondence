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
        public async Task Process(int batchCount, DateTimeOffset lastProcessed)
        {
            var enqueuedJobs = JobStorage.Current.GetMonitoringApi().EnqueuedCount(HangfireQueues.Migration);
            if (enqueuedJobs > batchCount * 5)
            {
                // If there are more than 5 batches worth of jobs already enqueued, we should wait before enqueuing more to avoid overwhelming the system
                logger.LogWarning(
                    "Migration queue has {EnqueuedJobs} jobs (threshold: {Threshold}). " +
                    "Delaying next batch by 1 minute to prevent queue overflow. " +
                    "Current processing threshold: {LastProcessed}",
                    enqueuedJobs,
                    batchCount * 5,
                    lastProcessed);

                backgroundJobClient.Schedule<MigrateNotificationEventsBatchHandler>(
                    HangfireQueues.Migration, 
                    handler => handler.Process(batchCount, lastProcessed), 
                    DateTime.UtcNow.AddMinutes(1));
            }
            else
            {
                var batch = await notificationRepository.GetSyncedNotificationsWithoutDialogActivityBatch(
                    batchCount, 
                    lastProcessed, 
                    CancellationToken.None);

                if (batch.Count == 0)
                {
                    logger.LogInformation("No more notification events to process. Migration of notification events is complete.");
                    return; // No more events to process
                }

                // Calculate actual date range of notifications in this batch
                var oldestNotification = batch.Min(n => n.NotificationSent);
                var newestNotification = batch.Max(n => n.NotificationSent);

                logger.LogInformation(
                    "Processing batch of {Count} notification events. " +
                    "Date range: {OldestDate} to {NewestDate}. " +
                    "Next batch will process notifications older than {NextThreshold}",
                    batch.Count,
                    oldestNotification,
                    newestNotification,
                    oldestNotification);

                foreach (var notification in batch)
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(
                        HangfireQueues.Migration, 
                        service => service.AddNotificationActivity(notification.Id, CancellationToken.None));
                }

                // Process in reverse chronological order (newest to oldest)
                var lastProcessedInBatch = oldestNotification ?? lastProcessed;

                backgroundJobClient.Enqueue<MigrateNotificationEventsBatchHandler>(
                    HangfireQueues.Migration, 
                    handler => handler.Process(batchCount, (DateTimeOffset)lastProcessedInBatch));
            }
        }
    }
}
