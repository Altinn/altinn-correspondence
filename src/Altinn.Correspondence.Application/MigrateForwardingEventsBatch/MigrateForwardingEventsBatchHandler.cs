using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.MigrateForwardingEventsBatch
{
    public class MigrateForwardingEventsBatchHandler(ICorrespondenceForwardingEventRepository forwardingEventRepository, IBackgroundJobClient backgroundJobClient, ILogger<MigrateForwardingEventsBatchHandler> logger)
    {
        public async Task Process(int batchCount, DateTimeOffset lastProcessed)
        {
            var enqueuedJobs = JobStorage.Current.GetMonitoringApi().EnqueuedCount(HangfireQueues.Migration);
            if (enqueuedJobs > batchCount * 5)
            {
                // If there are more than 5 batches worth of jobs already enqueued, we should wait before enqueuing more to avoid overwhelming the system
                backgroundJobClient.Schedule<MigrateForwardingEventsBatchHandler>(HangfireQueues.Migration, handler => handler.Process(batchCount, lastProcessed), DateTime.UtcNow.AddMinutes(1));
            } else
            {
                var batch = await forwardingEventRepository.GetForwardingEventsWithoutDialogActivityBatch(batchCount, lastProcessed, CancellationToken.None);
                if (batch.Count == 0)
                {
                    logger.LogInformation("No more forwarding events to process. Migration of forwarding events is complete.");
                    return; // No more events to process
                }
                foreach (var forwardingEvent in batch)
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(HangfireQueues.Migration, service => service.AddForwardingEvent(forwardingEvent.Id, CancellationToken.None));
                }
                var lastProcessedInBatch = batch.Count > 0 ? batch.Min(e => e.ForwardedOnDate) : lastProcessed;
                backgroundJobClient.Enqueue<MigrateForwardingEventsBatchHandler>(HangfireQueues.Migration, handler => handler.Process(batchCount, lastProcessedInBatch));
            }
        }
    }
}
