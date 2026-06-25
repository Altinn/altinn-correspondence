namespace Altinn.Correspondence.Application.BatchJobs;

public class ChainedBatchJobSettings
{
    public required string JobName { get; init; }

    public required int BatchSize { get; init; }

    /// <summary>Queue for per-entity worker jobs (heavy Dialogporten calls).</summary>
    public string WorkerQueue { get; init; } = ChainedBatchJobQueues.Worker;

    /// <summary>Queue for orchestrator jobs (fetch, enqueue next batch, reschedule).</summary>
    public string OrchestratorQueue { get; init; } = ChainedBatchJobQueues.Orchestrator;

    /// <summary>Queue whose depth is checked before fetching the next batch.</summary>
    public string BackpressureMonitorQueue { get; init; } = ChainedBatchJobQueues.Worker;

    public int BackpressureLimit { get; init; }

    public TimeSpan BackpressureRescheduleDelay { get; init; } = TimeSpan.FromMinutes(1);
}
