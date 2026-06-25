namespace Altinn.Correspondence.Application.BatchJobs;

public enum ChainedBatchJobPhase
{
    Running,
    WaitingForBackpressure,
    Completed,
}
