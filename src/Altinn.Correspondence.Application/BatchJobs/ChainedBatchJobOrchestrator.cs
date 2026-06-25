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
        ChainedBatchJobFetchResult<TItem> fetchResult;
        try
        {
            fetchResult = await definition.FetchBatchAsync(state, cancellationToken);
        }
        catch (Exception ex) when (IsFetchTimeout(ex, cancellationToken))
        {
            logger.LogWarning(
                ex,
                "{JobName}: database fetch timed out, rescheduling in {Delay}",
                settings.JobName,
                settings.BackpressureRescheduleDelay);

            await progressReporter.ReportAsync(
                settings.JobName,
                ChainedBatchJobPhase.FetchFailed,
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

        var hasProcessBatchAsync = definition.ProcessBatchAsync is not null;
        var hasEnqueueWorkerJob = definition.EnqueueWorkerJob is not null;
        if (hasProcessBatchAsync == hasEnqueueWorkerJob)
        {
            throw new InvalidOperationException(
                hasProcessBatchAsync
                    ? $"{settings.JobName} must define exactly one of ProcessBatchAsync or EnqueueWorkerJob, not both."
                    : $"{settings.JobName} must define either ProcessBatchAsync or EnqueueWorkerJob.");
        }

        var processedState = hasProcessBatchAsync
            ? await definition.ProcessBatchAsync!(state, fetchResult.Items, cancellationToken)
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

        if (hasEnqueueWorkerJob)
        {
            foreach (var item in fetchResult.Items)
            {
                definition.EnqueueWorkerJob!(item, state);
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

    private static bool IsFetchTimeout(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return ex switch
        {
            TimeoutException => true,
            OperationCanceledException => true,
            _ => ex.InnerException is not null && IsFetchTimeout(ex.InnerException, cancellationToken),
        };
    }
}
