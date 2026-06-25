namespace Altinn.Correspondence.Application.BatchJobs;

public class ChainedBatchJobDefinition<TState, TItem>
{
    public required ChainedBatchJobSettings Settings { get; init; }

    public required Func<TState, CancellationToken, Task<ChainedBatchJobFetchResult<TItem>>> FetchBatchAsync { get; init; }

    public required Func<TItem, KeysetCursor> GetCursorFromItem { get; init; }

    public required Func<TState, KeysetCursor, int, TState> CreateNextState { get; init; }

    public required Action<TState> EnqueueNextBatch { get; init; }

    public required Action<TState> RescheduleBatch { get; init; }

    public Action<TItem, TState>? EnqueueWorkerJob { get; init; }

    public Func<TState, IReadOnlyList<TItem>, CancellationToken, Task<TState>>? ProcessBatchAsync { get; init; }

    public Action<TState>? OnComplete { get; init; }

    public Func<TState, int>? ResolveBackpressureLimit { get; init; }
}
