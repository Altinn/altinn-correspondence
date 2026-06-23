namespace Altinn.Correspondence.Application.BatchJobs;

public record ChainedBatchJobProgress
{
    public required string JobName { get; init; }

    public required ChainedBatchJobPhase Phase { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? CursorCreated { get; init; }

    public Guid? CursorId { get; init; }

    public int? LastBatchItemCount { get; init; }

    public bool? HasMoreBatches { get; init; }

    public long? WorkerQueueDepth { get; init; }

    public int? BackpressureLimit { get; init; }

    public IReadOnlyDictionary<string, object?> Metrics { get; init; } = new Dictionary<string, object?>();
}
