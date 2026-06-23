using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.BatchJobs;

public class ChainedBatchJobOrchestrator(
    ILogger<ChainedBatchJobOrchestrator> logger,
    ChainedBatchJobProgressReporter progressReporter)
{
    public async Task RunBatchAsync<TState, TItem>(
        TState state,
        ChainedBatchJobDefinition<TState, TItem> definition,
        CancellationToken cancellationToken)
    {
        var settings = definition.Settings;

        var backpressureLimit = definition.ResolveBackpressureLimit?.Invoke(state) ?? settings.BackpressureLimit;

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

            await progressReporter.ReportAsync(
                settings.JobName,
                ChainedBatchJobPhase.WaitingForBackpressure,
                state,
                lastBatchItemCount: null,
                hasMoreBatches: null,
                workerQueueDepth: enqueuedJobs,
                backpressureLimit: backpressureLimit,
                batchEndCursor: null,
                definition.BuildProgressMetrics,
                cancellationToken);

            definition.RescheduleBatch(state);
            return;
        }

        logger.LogInformation("{JobName}: querying database", settings.JobName);
        var fetchResult = await definition.FetchBatchAsync(state, cancellationToken);
        logger.LogInformation("{JobName}: found {Count} items", settings.JobName, fetchResult.Items.Count);

        if (fetchResult.Items.Count == 0)
        {
            logger.LogInformation("{JobName}: no more items to process", settings.JobName);
            await progressReporter.ReportAsync(
                settings.JobName,
                ChainedBatchJobPhase.Completed,
                state,
                lastBatchItemCount: 0,
                hasMoreBatches: false,
                workerQueueDepth: enqueuedJobs,
                backpressureLimit: backpressureLimit,
                batchEndCursor: null,
                definition.BuildProgressMetrics,
                cancellationToken);
            definition.OnComplete?.Invoke(state);
            return;
        }

        var processedState = definition.ProcessBatchAsync is not null
            ? await definition.ProcessBatchAsync(state, fetchResult.Items, cancellationToken)
            : state;

        KeysetCursor? batchEndCursor = null;
        if (fetchResult.HasMoreBatches)
        {
            var last = fetchResult.Items[^1];
            batchEndCursor = definition.GetCursorFromItem(last);
            var nextState = definition.CreateNextState(processedState, batchEndCursor, fetchResult.Items.Count);
            logger.LogInformation("{JobName}: enqueuing next batch after cursor {CursorId}", settings.JobName, batchEndCursor.Id);
            definition.EnqueueNextBatch(nextState);
        }
        else
        {
            await progressReporter.ReportAsync(
                settings.JobName,
                ChainedBatchJobPhase.Completed,
                processedState,
                lastBatchItemCount: fetchResult.Items.Count,
                hasMoreBatches: false,
                workerQueueDepth: enqueuedJobs,
                backpressureLimit: backpressureLimit,
                batchEndCursor: definition.GetCursorFromItem(fetchResult.Items[^1]),
                definition.BuildProgressMetrics,
                cancellationToken);
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

        if (fetchResult.HasMoreBatches)
        {
            await progressReporter.ReportAsync(
                settings.JobName,
                ChainedBatchJobPhase.Running,
                processedState,
                lastBatchItemCount: fetchResult.Items.Count,
                hasMoreBatches: true,
                workerQueueDepth: enqueuedJobs,
                backpressureLimit: backpressureLimit,
                batchEndCursor: batchEndCursor,
                definition.BuildProgressMetrics,
                cancellationToken);
        }
    }
}
