namespace Altinn.Correspondence.Application.BatchJobs;

public interface IChainedBatchJobProgressStore
{
    Task SetAsync(ChainedBatchJobProgress progress, CancellationToken cancellationToken = default);

    Task<ChainedBatchJobProgress?> GetAsync(string jobName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChainedBatchJobProgress>> GetAllAsync(CancellationToken cancellationToken = default);
}
