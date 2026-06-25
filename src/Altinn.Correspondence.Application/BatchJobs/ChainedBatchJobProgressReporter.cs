using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.BatchJobs;

public class ChainedBatchJobProgressReporter(
    ILogger<ChainedBatchJobProgressReporter> logger,
    IChainedBatchJobProgressStore progressStore)
{
    public async Task ReportAsync<TState>(
        string jobName,
        ChainedBatchJobPhase phase,
        TState state,
        int? lastBatchItemCount,
        bool? hasMoreBatches,
        long? workerQueueDepth,
        int? backpressureLimit,
        KeysetCursor? batchEndCursor,
        Func<TState, IReadOnlyDictionary<string, object?>>? buildMetrics,
        CancellationToken cancellationToken)
    {
        var (cursorCreated, cursorId) = ResolveCursor(state, batchEndCursor);
        var metrics = buildMetrics?.Invoke(state) ?? new Dictionary<string, object?>();

        var progress = new ChainedBatchJobProgress
        {
            JobName = jobName,
            Phase = phase,
            UpdatedAt = DateTimeOffset.UtcNow,
            CursorCreated = cursorCreated,
            CursorId = cursorId,
            LastBatchItemCount = lastBatchItemCount,
            HasMoreBatches = hasMoreBatches,
            WorkerQueueDepth = workerQueueDepth,
            BackpressureLimit = backpressureLimit,
            Metrics = metrics,
        };

        logger.LogInformation(
            "ChainedBatchJobStatus {JobName} phase={Phase} cursorCreated={CursorCreated} cursorId={CursorId} lastBatchItems={LastBatchItemCount} hasMoreBatches={HasMoreBatches} workerQueueDepth={WorkerQueueDepth} backpressureLimit={BackpressureLimit} metrics={@Metrics}",
            progress.JobName,
            progress.Phase,
            progress.CursorCreated,
            progress.CursorId,
            progress.LastBatchItemCount,
            progress.HasMoreBatches,
            progress.WorkerQueueDepth,
            progress.BackpressureLimit,
            progress.Metrics);

        await progressStore.SetAsync(progress, cancellationToken);
    }

    private static (DateTimeOffset? CursorCreated, Guid? CursorId) ResolveCursor<TState>(
        TState state,
        KeysetCursor? batchEndCursor)
    {
        if (batchEndCursor is not null)
        {
            return (batchEndCursor.Created, batchEndCursor.Id);
        }

        if (state is IChainedBatchJobCursorState cursorState)
        {
            return (cursorState.CursorCreated, cursorState.CursorId);
        }

        return (null, null);
    }
}
