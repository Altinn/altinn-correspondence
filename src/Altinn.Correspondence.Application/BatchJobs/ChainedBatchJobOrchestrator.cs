using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.BatchJobs;

public class ChainedBatchJobOrchestrator(ILogger<ChainedBatchJobOrchestrator> logger)
{
    public async Task RunBatchAsync<TState, TItem>(
        TState state,
        ChainedBatchJobDefinition<TState, TItem> definition,
        CancellationToken cancellationToken)
    {
        var settings = definition.Settings;

        var backpressureLimit = definition.ResolveBackpressureLimit?.Invoke(state) ?? settings.BackpressureLimit;
        if (backpressureLimit <= 0)
        {
            throw new InvalidOperationException(
                $"{settings.JobName} has invalid backpressure limit {backpressureLimit}. It must be > 0.");
        }

        var enqueuedJobs = JobStorage.Current.GetMonitoringApi().EnqueuedCount(settings.BackpressureMonitorQueue);
        if (enqueuedJobs >= backpressureLimit)
        {
            logger.LogInformation(
                "{JobName}: queue {Queue} has {EnqueuedJobs} jobs (limit {Limit}), rescheduling in {Delay}",
                settings.JobName,
                settings.BackpressureMonitorQueue,
                enqueuedJobs,
                backpressureLimit,
                settings.BackpressureRescheduleDelay);

            definition.RescheduleBatch(state);
            return;
        }

        logger.LogInformation("{JobName}: querying database", settings.JobName);
        var fetchResult = await definition.FetchBatchAsync(state, cancellationToken);
        logger.LogInformation("{JobName}: found {Count} items", settings.JobName, fetchResult.Items.Count);

        if (fetchResult.Items.Count == 0)
        {
            logger.LogInformation("{JobName}: no more items to process", settings.JobName);
            definition.OnComplete?.Invoke(state);
            return;
        }

        var processedState = definition.ProcessBatchAsync is not null
            ? await definition.ProcessBatchAsync(state, fetchResult.Items, cancellationToken)
            : state;

        if (fetchResult.HasMoreBatches)
        {
            var last = fetchResult.Items[^1];
            var cursor = definition.GetCursorFromItem(last);
            var nextState = definition.CreateNextState(processedState, cursor, fetchResult.Items.Count);
            logger.LogInformation("{JobName}: enqueuing next batch after cursor {CursorId}", settings.JobName, cursor.Id);
            definition.EnqueueNextBatch(nextState);
        }
        else
        {
            definition.OnComplete?.Invoke(processedState);
        }

        if (definition.ProcessBatchAsync is null)
        {
            if (definition.EnqueueWorkerJob is null)
            {
                throw new InvalidOperationException($"{settings.JobName} must define either ProcessBatchAsync or EnqueueWorkerJob.");
            }

            foreach (var item in fetchResult.Items)
            {
                definition.EnqueueWorkerJob(item, state);
            }

            logger.LogInformation("{JobName}: finished queuing {Count} worker jobs", settings.JobName, fetchResult.Items.Count);
        }
    }
}
