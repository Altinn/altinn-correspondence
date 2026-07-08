using Altinn.Correspondence.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.Correspondence.Application.BatchJobs;

public class HybridCacheChainedBatchJobProgressStore(IHybridCacheWrapper cache) : IChainedBatchJobProgressStore
{
    private const string KeyPrefix = "chained-batch-job-progress:";
    private const string IndexKey = "chained-batch-job-progress:job-names";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromDays(90),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
    };

    public async Task SetAsync(ChainedBatchJobProgress progress, CancellationToken cancellationToken = default)
    {
        await cache.SetAsync(
            KeyPrefix + progress.JobName,
            progress,
            CacheOptions,
            cancellationToken: cancellationToken);

        var jobNames = await cache.GetAsync<List<string>>(IndexKey, cancellationToken: cancellationToken) ?? [];
        if (!jobNames.Contains(progress.JobName, StringComparer.OrdinalIgnoreCase))
        {
            jobNames.Add(progress.JobName);
            await cache.SetAsync(IndexKey, jobNames, CacheOptions, cancellationToken: cancellationToken);
        }
    }

    public Task<ChainedBatchJobProgress?> GetAsync(string jobName, CancellationToken cancellationToken = default) =>
        cache.GetAsync<ChainedBatchJobProgress>(KeyPrefix + jobName, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<ChainedBatchJobProgress>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var jobNames = await cache.GetAsync<List<string>>(IndexKey, cancellationToken: cancellationToken) ?? [];
        var results = new List<ChainedBatchJobProgress>(jobNames.Count);

        foreach (var jobName in jobNames)
        {
            var progress = await GetAsync(jobName, cancellationToken);
            if (progress is not null)
            {
                results.Add(progress);
            }
        }

        return results;
    }
}
