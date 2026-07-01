namespace Altinn.Correspondence.Application.BatchJobs;

public record ChainedBatchJobFetchResult<TItem>(IReadOnlyList<TItem> Items, bool HasMoreBatches);
