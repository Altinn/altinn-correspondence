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

                logger.LogInformation(
                    "Processing {Count} notification events. Last processed date: {LastProcessed}", 
                    batch.Count, 
                    lastProcessed);

                foreach (var notification in batch)
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(
                        HangfireQueues.Migration, 
                        service => service.AddNotificationActivity(notification.Id, CancellationToken.None));
                }

                // Process in reverse chronological order (newest to oldest)
                var lastProcessedInBatch = batch.Count > 0 
                    ? batch.Min(n => n.NotificationSent ?? n.RequestedSendTime) 
                    : lastProcessed;

                backgroundJobClient.Enqueue<MigrateNotificationEventsBatchHandler>(
                    HangfireQueues.Migration, 
                    handler => handler.Process(batchCount, lastProcessedInBatch));
            }
        }
    }
}
