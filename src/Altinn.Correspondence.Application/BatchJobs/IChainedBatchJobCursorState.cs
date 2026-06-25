namespace Altinn.Correspondence.Application.BatchJobs;

/// <summary>
/// Optional state shape for automatic cursor reporting in chained batch jobs.
/// </summary>
public interface IChainedBatchJobCursorState
{
    DateTimeOffset? CursorCreated { get; }

    Guid? CursorId { get; }
}
