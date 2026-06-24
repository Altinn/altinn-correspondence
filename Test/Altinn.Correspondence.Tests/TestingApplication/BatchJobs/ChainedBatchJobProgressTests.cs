using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingApplication.BatchJobs;

public class ChainedBatchJobProgressReporterTests
{
    [Fact]
    public async Task ReportAsync_UsesBatchEndCursor_WhenProvided()
    {
        var store = new InMemoryChainedBatchJobProgressStore();
        var reporter = new ChainedBatchJobProgressReporter(
            NullLogger<ChainedBatchJobProgressReporter>.Instance,
            store);

        var batchCursor = new KeysetCursor(DateTimeOffset.UtcNow, Guid.NewGuid());
        var state = new TestCursorState
        {
            CursorCreated = DateTimeOffset.UtcNow.AddDays(-10),
            CursorId = Guid.NewGuid(),
        };

        await reporter.ReportAsync(
            "TestJob",
            ChainedBatchJobPhase.Running,
            state,
            lastBatchItemCount: 5,
            hasMoreBatches: true,
            workerQueueDepth: 12,
            backpressureLimit: 20,
            batchEndCursor: batchCursor,
            buildMetrics: s => new Dictionary<string, object?> { ["count"] = s.Counter },
            CancellationToken.None);

        var progress = Assert.Single(store.All);
        Assert.Equal(batchCursor.Created, progress.CursorCreated);
        Assert.Equal(batchCursor.Id, progress.CursorId);
        Assert.Equal(5, progress.LastBatchItemCount);
        Assert.Equal(12, progress.WorkerQueueDepth);
        Assert.Equal(1, progress.Metrics["count"]);
    }

    [Fact]
    public async Task ReportAsync_FallsBackToCursorState_WhenBatchEndCursorMissing()
    {
        var store = new InMemoryChainedBatchJobProgressStore();
        var reporter = new ChainedBatchJobProgressReporter(
            NullLogger<ChainedBatchJobProgressReporter>.Instance,
            store);

        var state = new TestCursorState
        {
            CursorCreated = DateTimeOffset.UtcNow.AddDays(-3),
            CursorId = Guid.NewGuid(),
        };

        await reporter.ReportAsync(
            "TestJob",
            ChainedBatchJobPhase.WaitingForBackpressure,
            state,
            lastBatchItemCount: null,
            hasMoreBatches: null,
            workerQueueDepth: 99,
            backpressureLimit: 50,
            batchEndCursor: null,
            buildMetrics: null,
            CancellationToken.None);

        var progress = Assert.Single(store.All);
        Assert.Equal(state.CursorCreated, progress.CursorCreated);
        Assert.Equal(state.CursorId, progress.CursorId);
        Assert.Equal(ChainedBatchJobPhase.WaitingForBackpressure, progress.Phase);
    }

    private sealed class TestCursorState : IChainedBatchJobCursorState
    {
        public DateTimeOffset? CursorCreated { get; init; }

        public Guid? CursorId { get; init; }

        public int Counter { get; init; } = 1;
    }
}

public class HybridCacheChainedBatchJobProgressStoreTests
{
    [Fact]
    public async Task SetAsync_StoresProgressAndIndexesJobName()
    {
        var cache = new Mock<IHybridCacheWrapper>();
        var storedProgress = new Dictionary<string, ChainedBatchJobProgress>(StringComparer.OrdinalIgnoreCase);
        List<string>? storedIndex = null;

        cache.Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<ChainedBatchJobProgress>(),
                It.IsAny<HybridCacheEntryOptions?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ChainedBatchJobProgress, HybridCacheEntryOptions?, IEnumerable<string>?, CancellationToken>(
                (key, value, _, _, _) => storedProgress[key] = value)
            .Returns(Task.CompletedTask);

        cache.Setup(c => c.GetAsync<List<string>>(
                "chained-batch-job-progress:job-names",
                It.IsAny<HybridCacheEntryOptions?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedIndex);

        cache.Setup(c => c.SetAsync(
                "chained-batch-job-progress:job-names",
                It.IsAny<List<string>>(),
                It.IsAny<HybridCacheEntryOptions?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, List<string>, HybridCacheEntryOptions?, IEnumerable<string>?, CancellationToken>(
                (_, value, _, _, _) => storedIndex = value)
            .Returns(Task.CompletedTask);

        var store = new HybridCacheChainedBatchJobProgressStore(cache.Object);
        var progress = new ChainedBatchJobProgress
        {
            JobName = "MakeCorrespondenceAvailable",
            Phase = ChainedBatchJobPhase.Running,
            UpdatedAt = DateTimeOffset.UtcNow,
            CursorCreated = DateTimeOffset.UtcNow.AddYears(-1),
            CursorId = Guid.NewGuid(),
        };

        await store.SetAsync(progress);

        Assert.True(storedProgress.ContainsKey("chained-batch-job-progress:MakeCorrespondenceAvailable"));
        Assert.NotNull(storedIndex);
        Assert.Contains("MakeCorrespondenceAvailable", storedIndex);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllStoredProgressSnapshots()
    {
        var cache = new Mock<IHybridCacheWrapper>();
        var progress = new ChainedBatchJobProgress
        {
            JobName = "UpdateOldCorrespondencesWithDownloadAll",
            Phase = ChainedBatchJobPhase.Completed,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        cache.Setup(c => c.GetAsync<List<string>>(
                "chained-batch-job-progress:job-names",
                It.IsAny<HybridCacheEntryOptions?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["UpdateOldCorrespondencesWithDownloadAll"]);

        cache.Setup(c => c.GetAsync<ChainedBatchJobProgress>(
                "chained-batch-job-progress:UpdateOldCorrespondencesWithDownloadAll",
                It.IsAny<HybridCacheEntryOptions?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        var store = new HybridCacheChainedBatchJobProgressStore(cache.Object);

        var all = await store.GetAllAsync();

        Assert.Single(all);
        Assert.Equal(progress.JobName, all[0].JobName);
        Assert.Equal(ChainedBatchJobPhase.Completed, all[0].Phase);
    }
}

internal sealed class InMemoryChainedBatchJobProgressStore : IChainedBatchJobProgressStore
{
    private readonly Dictionary<string, ChainedBatchJobProgress> _progressByJobName = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ChainedBatchJobProgress> All => _progressByJobName.Values.ToList();

    public Task SetAsync(ChainedBatchJobProgress progress, CancellationToken cancellationToken = default)
    {
        _progressByJobName[progress.JobName] = progress;
        return Task.CompletedTask;
    }

    public Task<ChainedBatchJobProgress?> GetAsync(string jobName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_progressByJobName.GetValueOrDefault(jobName));

    public Task<IReadOnlyList<ChainedBatchJobProgress>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChainedBatchJobProgress>>(All);
}
