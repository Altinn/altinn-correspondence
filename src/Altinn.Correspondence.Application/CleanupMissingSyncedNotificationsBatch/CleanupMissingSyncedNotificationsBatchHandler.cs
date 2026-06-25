using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.CleanupMissingSyncedNotificationsBatch
{
    public class CleanupMissingSyncedNotificationsBatchHandler(
        IBackgroundJobClient backgroundJobClient,
        ChainedBatchJobOrchestrator orchestrator,
        CleanupMissingSyncedNotificationsBatchJob batchJob,
        ILogger<CleanupMissingSyncedNotificationsBatchHandler> logger)
    {
        /// <summary>
        /// HTTP entry point - enqueues the first orchestrator batch job
        /// </summary>
        public Task Process(int batchCount, DateTimeOffset lastProcessedTimestamp, Guid? lastProcessedId = null)
        {
            var sanitizedLastProcessedId = SanitizeForLog(lastProcessedId?.ToString());

            logger.LogInformation(
                "Starting notification event migration. Batch size: {BatchSize}, Starting cursor: {Timestamp} / {Id}",
                batchCount,
                lastProcessedTimestamp,
                sanitizedLastProcessedId);

            var request = new CleanupMissingSyncedNotificationsBatchRequest
            {
                BatchSize = batchCount,
                CursorNotificationSent = lastProcessedTimestamp,
                CursorId = lastProcessedId
            };

            var jobId = backgroundJobClient.Enqueue<CleanupMissingSyncedNotificationsBatchHandler>(
                ChainedBatchJobQueues.Orchestrator,
                handler => handler.ExecuteBatch(request, CancellationToken.None));

            logger.LogInformation("Notification event migration orchestrator job {JobId} enqueued", jobId);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Hangfire orchestrator method - runs one batch iteration
        /// </summary>
        [AutomaticRetry(Attempts = 0)]
        public async Task ExecuteBatch(CleanupMissingSyncedNotificationsBatchRequest request, CancellationToken cancellationToken)
        {
            await orchestrator.RunBatchAsync(request, batchJob.CreateDefinition(), cancellationToken);
        }

        /// <summary>
        /// Worker method - processes one correspondence group with duplicate checking
        /// This is called by IDialogportenService.AddNotificationActivitiesWithDuplicateCheck
        /// but included here for reference as it's enqueued by the batch job
        /// </summary>
        [AutomaticRetry(Attempts = 0)]
        public async Task ProcessCorrespondenceNotifications(Guid correspondenceId, List<Guid> notificationIds, CancellationToken cancellationToken)
        {
            // This method signature matches what's called in the batch job definition
            // The actual implementation is in DialogportenService.AddNotificationActivitiesWithDuplicateCheck
            throw new NotImplementedException("This is handled by IDialogportenService");
        }

        private static string SanitizeForLog(string? value)
        {
            return value?
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty) ?? string.Empty;
        }
    }
}
