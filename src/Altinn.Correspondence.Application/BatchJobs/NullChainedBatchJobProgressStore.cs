namespace Altinn.Correspondence.Application.BatchJobs;

public sealed class NullChainedBatchJobProgressStore : IChainedBatchJobProgressStore
{
    public Task SetAsync(ChainedBatchJobProgress progress, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<ChainedBatchJobProgress?> GetAsync(string jobName, CancellationToken cancellationToken = default) =>
        Task.FromResult<ChainedBatchJobProgress?>(null);

    public Task<IReadOnlyList<ChainedBatchJobProgress>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChainedBatchJobProgress>>([]);
}
