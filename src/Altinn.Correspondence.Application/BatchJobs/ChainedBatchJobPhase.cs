namespace Altinn.Correspondence.Application.BatchJobs;

public enum ChainedBatchJobPhase
{
    Running,
    WaitingForBackpressure,
    FetchFailed,
    Completed,
}
