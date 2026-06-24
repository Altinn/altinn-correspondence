using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;

namespace Altinn.Correspondence.Tests.TestingApplication.BatchJobs;

public class ChainedBatchJobOrchestratorTests : IDisposable
{
    public ChainedBatchJobOrchestratorTests()
    {
        JobStorage.Current = new MemoryStorage();
    }

    public void Dispose()
    {
        JobStorage.Current = null!;
    }

    public static void NoOpHangfireJob()
    {
    }

    [Fact]
    public async Task RunBatchAsync_EmptyFetch_ReportsCompletedAndInvokesOnComplete()
    {
        var progressStore = new InMemoryChainedBatchJobProgressStore();
        var orchestrator = CreateOrchestrator(progressStore);
        var onCompleteInvoked = false;

        var definition = CreateFanOutDefinition(
            fetchBatchAsync: (_, _) => Task.FromResult(new ChainedBatchJobFetchResult<TestItem>([], false)),
            onComplete: _ => onCompleteInvoked = true);

        await orchestrator.RunBatchAsync(new TestState(), definition, CancellationToken.None);

        Assert.True(onCompleteInvoked);
        var progress = Assert.Single(progressStore.All);
        Assert.Equal(ChainedBatchJobPhase.Completed, progress.Phase);
        Assert.Equal(0, progress.LastBatchItemCount);
        Assert.False(progress.HasMoreBatches);
    }

    [Fact]
    public async Task RunBatchAsync_FanOut_EnqueuesWorkerJobsAndChainsNextBatch()
    {
        var progressStore = new InMemoryChainedBatchJobProgressStore();
        var orchestrator = CreateOrchestrator(progressStore);
        var workerIds = new List<Guid>();
        TestState? chainedState = null;

        var item1 = new TestItem(DateTimeOffset.UtcNow.AddDays(-2), Guid.NewGuid());
        var item2 = new TestItem(DateTimeOffset.UtcNow.AddDays(-1), Guid.NewGuid());

        var definition = CreateFanOutDefinition(
            fetchBatchAsync: (_, _) => Task.FromResult(
                new ChainedBatchJobFetchResult<TestItem>([item1, item2], true)),
            enqueueWorkerJob: (item, _) => workerIds.Add(item.Id),
            enqueueNextBatch: state => chainedState = state);

        await orchestrator.RunBatchAsync(new TestState(), definition, CancellationToken.None);

        Assert.Equal([item1.Id, item2.Id], workerIds);
        Assert.NotNull(chainedState);
        Assert.Equal(item2.Created, chainedState.CursorCreated);
        Assert.Equal(item2.Id, chainedState.CursorId);

        var progress = Assert.Single(progressStore.All);
        Assert.Equal(ChainedBatchJobPhase.Running, progress.Phase);
        Assert.Equal(2, progress.LastBatchItemCount);
        Assert.True(progress.HasMoreBatches);
        Assert.Equal(item2.Created, progress.CursorCreated);
        Assert.Equal(item2.Id, progress.CursorId);
    }

    [Fact]
    public async Task RunBatchAsync_FinalBatch_ReportsCompletedWithEndCursor()
    {
        var progressStore = new InMemoryChainedBatchJobProgressStore();
        var orchestrator = CreateOrchestrator(progressStore);
        var onCompleteInvoked = false;
        var nextBatchInvoked = false;

        var item = new TestItem(DateTimeOffset.UtcNow, Guid.NewGuid());

        var definition = CreateFanOutDefinition(
            fetchBatchAsync: (_, _) => Task.FromResult(
                new ChainedBatchJobFetchResult<TestItem>([item], false)),
            enqueueNextBatch: _ => nextBatchInvoked = true,
            onComplete: _ => onCompleteInvoked = true);

        await orchestrator.RunBatchAsync(new TestState(), definition, CancellationToken.None);

        Assert.True(onCompleteInvoked);
        Assert.False(nextBatchInvoked);

        var progress = Assert.Single(progressStore.All);
        Assert.Equal(ChainedBatchJobPhase.Completed, progress.Phase);
        Assert.Equal(item.Created, progress.CursorCreated);
        Assert.Equal(item.Id, progress.CursorId);
    }

    [Fact]
    public async Task RunBatchAsync_Backpressure_ReschedulesWithoutFetching()
    {
        var progressStore = new InMemoryChainedBatchJobProgressStore();
        var orchestrator = CreateOrchestrator(progressStore);
        var fetchInvoked = false;
        var rescheduled = false;
        var backgroundJobClient = new BackgroundJobClient();

        for (var i = 0; i < 3; i++)
        {
            backgroundJobClient.Create(
                Job.FromExpression(() => NoOpHangfireJob()),
                new EnqueuedState(HangfireQueues.Migration));
        }

        var definition = CreateFanOutDefinition(
            backpressureLimit: 2,
            fetchBatchAsync: (_, _) =>
            {
                fetchInvoked = true;
                return Task.FromResult(new ChainedBatchJobFetchResult<TestItem>([], false));
            },
            rescheduleBatch: _ => rescheduled = true);

        var state = new TestState
        {
            CursorCreated = DateTimeOffset.UtcNow.AddDays(-1),
            CursorId = Guid.NewGuid(),
        };

        await orchestrator.RunBatchAsync(state, definition, CancellationToken.None);

        Assert.True(rescheduled);
        Assert.False(fetchInvoked);

        var progress = Assert.Single(progressStore.All);
        Assert.Equal(ChainedBatchJobPhase.WaitingForBackpressure, progress.Phase);
        Assert.Equal(state.CursorCreated, progress.CursorCreated);
        Assert.Equal(state.CursorId, progress.CursorId);
        Assert.Equal(3, progress.WorkerQueueDepth);
        Assert.Equal(2, progress.BackpressureLimit);
    }

    [Fact]
    public async Task RunBatchAsync_ProcessBatchAsync_UsesUpdatedStateForProgressMetrics()
    {
        var progressStore = new InMemoryChainedBatchJobProgressStore();
        var orchestrator = CreateOrchestrator(progressStore);
        var item = new TestItem(DateTimeOffset.UtcNow, Guid.NewGuid());

        var definition = new ChainedBatchJobDefinition<TestState, TestItem>
        {
            Settings = new ChainedBatchJobSettings
            {
                JobName = "FilteredBatch",
                BatchSize = 1,
                BackpressureLimit = 100,
            },
            FetchBatchAsync = (_, _) => Task.FromResult(
                new ChainedBatchJobFetchResult<TestItem>([item], false)),
            GetCursorFromItem = item => new KeysetCursor(item.Created, item.Id),
            CreateNextState = (state, cursor, _) => state with
            {
                CursorCreated = cursor.Created,
                CursorId = cursor.Id,
            },
            EnqueueNextBatch = _ => { },
            RescheduleBatch = _ => { },
            ProcessBatchAsync = (state, items, _) =>
            {
                return Task.FromResult(state with { ProcessedCount = state.ProcessedCount + items.Count });
            },
            OnComplete = _ => { },
            BuildProgressMetrics = state => new Dictionary<string, object?>
            {
                ["processedCount"] = state.ProcessedCount,
            },
        };

        await orchestrator.RunBatchAsync(new TestState(), definition, CancellationToken.None);

        var progress = Assert.Single(progressStore.All);
        Assert.Equal(ChainedBatchJobPhase.Completed, progress.Phase);
        Assert.Equal(1, progress.Metrics["processedCount"]);
    }

    [Fact]
    public async Task RunBatchAsync_MissingWorkerAndProcess_Throws()
    {
        var orchestrator = CreateOrchestrator(new InMemoryChainedBatchJobProgressStore());
        var item = new TestItem(DateTimeOffset.UtcNow, Guid.NewGuid());

        var definition = new ChainedBatchJobDefinition<TestState, TestItem>
        {
            Settings = new ChainedBatchJobSettings
            {
                JobName = "Invalid",
                BatchSize = 1,
                BackpressureLimit = 100,
            },
            FetchBatchAsync = (_, _) => Task.FromResult(
                new ChainedBatchJobFetchResult<TestItem>([item], false)),
            GetCursorFromItem = item => new KeysetCursor(item.Created, item.Id),
            CreateNextState = (state, _, _) => state,
            EnqueueNextBatch = _ => { },
            RescheduleBatch = _ => { },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.RunBatchAsync(new TestState(), definition, CancellationToken.None));
    }

    [Fact]
    public async Task RunBatchAsync_FetchTimeoutException_ReschedulesWithoutAdvancingCursor()
    {
        var progressStore = new InMemoryChainedBatchJobProgressStore();
        var orchestrator = CreateOrchestrator(progressStore);
        var workerInvoked = false;
        var nextBatchInvoked = false;
        var rescheduled = false;

        var state = new TestState
        {
            CursorCreated = DateTimeOffset.UtcNow.AddDays(-5),
            CursorId = Guid.NewGuid(),
        };

        var definition = CreateFanOutDefinition(
            fetchBatchAsync: (_, _) => throw new TimeoutException("Database command timed out"),
            enqueueWorkerJob: (_, _) => workerInvoked = true,
            enqueueNextBatch: _ => nextBatchInvoked = true,
            rescheduleBatch: _ => rescheduled = true);

        await orchestrator.RunBatchAsync(state, definition, CancellationToken.None);

        Assert.True(rescheduled);
        Assert.False(workerInvoked);
        Assert.False(nextBatchInvoked);

        var progress = Assert.Single(progressStore.All);
        Assert.Equal(ChainedBatchJobPhase.FetchFailed, progress.Phase);
        Assert.Equal(state.CursorCreated, progress.CursorCreated);
        Assert.Equal(state.CursorId, progress.CursorId);
    }

    [Fact]
    public async Task RunBatchAsync_FetchOperationCanceledWithoutToken_ReschedulesWithoutAdvancingCursor()
    {
        var progressStore = new InMemoryChainedBatchJobProgressStore();
        var orchestrator = CreateOrchestrator(progressStore);
        var rescheduled = false;

        var state = new TestState
        {
            CursorCreated = DateTimeOffset.UtcNow.AddDays(-3),
            CursorId = Guid.NewGuid(),
        };

        var definition = CreateFanOutDefinition(
            fetchBatchAsync: (_, _) => throw new OperationCanceledException("Npgsql command timeout"),
            rescheduleBatch: _ => rescheduled = true);

        await orchestrator.RunBatchAsync(state, definition, CancellationToken.None);

        Assert.True(rescheduled);

        var progress = Assert.Single(progressStore.All);
        Assert.Equal(ChainedBatchJobPhase.FetchFailed, progress.Phase);
        Assert.Equal(state.CursorCreated, progress.CursorCreated);
        Assert.Equal(state.CursorId, progress.CursorId);
    }

    [Fact]
    public async Task RunBatchAsync_FetchCanceledByToken_PropagatesException()
    {
        var orchestrator = CreateOrchestrator(new InMemoryChainedBatchJobProgressStore());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var definition = CreateFanOutDefinition(
            fetchBatchAsync: (_, cancellationToken) =>
                throw new OperationCanceledException(cancellationToken));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            orchestrator.RunBatchAsync(new TestState(), definition, cts.Token));
    }

    [Fact]
    public async Task RunBatchAsync_FetchNonTransientFailure_PropagatesException()
    {
        var progressStore = new InMemoryChainedBatchJobProgressStore();
        var orchestrator = CreateOrchestrator(progressStore);
        var rescheduled = false;

        var definition = CreateFanOutDefinition(
            fetchBatchAsync: (_, _) => throw new InvalidOperationException("Unexpected database error"),
            rescheduleBatch: _ => rescheduled = true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.RunBatchAsync(new TestState(), definition, CancellationToken.None));

        Assert.False(rescheduled);
        Assert.Empty(progressStore.All);
    }

    private static ChainedBatchJobOrchestrator CreateOrchestrator(IChainedBatchJobProgressStore progressStore) =>
        new(
            NullLogger<ChainedBatchJobOrchestrator>.Instance,
            new ChainedBatchJobProgressReporter(
                NullLogger<ChainedBatchJobProgressReporter>.Instance,
                progressStore));

    private static ChainedBatchJobDefinition<TestState, TestItem> CreateFanOutDefinition(
        Func<TestState, CancellationToken, Task<ChainedBatchJobFetchResult<TestItem>>>? fetchBatchAsync = null,
        Action<TestItem, TestState>? enqueueWorkerJob = null,
        Action<TestState>? enqueueNextBatch = null,
        Action<TestState>? rescheduleBatch = null,
        Action<TestState>? onComplete = null,
        int backpressureLimit = 100)
    {
        return new ChainedBatchJobDefinition<TestState, TestItem>
        {
            Settings = new ChainedBatchJobSettings
            {
                JobName = "TestFanOut",
                BatchSize = 2,
                BackpressureLimit = backpressureLimit,
            },
            FetchBatchAsync = fetchBatchAsync ?? ((_, _) =>
                Task.FromResult(new ChainedBatchJobFetchResult<TestItem>([], false))),
            GetCursorFromItem = item => new KeysetCursor(item.Created, item.Id),
            CreateNextState = (state, cursor, _) => state with
            {
                CursorCreated = cursor.Created,
                CursorId = cursor.Id,
            },
            EnqueueWorkerJob = enqueueWorkerJob ?? ((_, _) => { }),
            EnqueueNextBatch = enqueueNextBatch ?? (_ => { }),
            RescheduleBatch = rescheduleBatch ?? (_ => { }),
            OnComplete = onComplete,
        };
    }

    private sealed record TestItem(DateTimeOffset Created, Guid Id);

    private sealed record TestState : IChainedBatchJobCursorState
    {
        public DateTimeOffset? CursorCreated { get; init; }

        public Guid? CursorId { get; init; }

        public int ProcessedCount { get; init; }
    }
}
