using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Storage.Monitoring;
using Npgsql;
using System.Collections.Concurrent;

namespace Altinn.Correspondence.Tests.Invariants;

// PrePublishJobFinalStateTests asserts that the jobs running before publish are configured with
// OnAttemptsExceeded = AttemptsExceededAction.Delete. That configuration only fixes the stuck-in-Initialized bug if
// Hangfire actually runs a JobContinuationOptions.OnAnyFinishedState continuation once the parent job has been deleted
// after exhausting its attempts - and it only explains the bug if the same continuation does not run while the parent is
// left in the Failed state. This test verifies both halves against a real Hangfire server and Postgres storage, so a
// Hangfire upgrade that changes the semantics is caught here instead of in production.
// See https://github.com/Altinn/altinn-correspondence/issues/2061
//
// The test deliberately does not boot the application - it only needs storage and a job activator - and it passes its own
// activator through BackgroundJobServerOptions instead of replacing the global JobActivator.Current that other tests use.
// The collection attribute keeps it serialized with the other tests that run a Hangfire server, so they do not compete
// for workers and connections.
[Collection(nameof(CustomWebApplicationTestsCollection))]
public class HangfireContinuationOnFinalStateTests
{
    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(90);

    // Time both parents are given to finish exhausting their attempts, and the Failed one to (incorrectly) trigger its
    // continuation.
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Continuation_RunsWhenParentIsDeletedByAttemptsExceeded_ButNotWhenParentIsLeftFailed()
    {
        var tracker = new ContinuationTracker();
        await using var dataSource = NpgsqlDataSource.Create(TestDbContextFactory.ConnectionString);

        var schemaName = $"hangfire_test_{Guid.NewGuid().ToString("N")[..8]}";
        var migrateConnection = await dataSource.OpenConnectionAsync();
        try
        {
            var createSchemaCommand = migrateConnection.CreateCommand();
            createSchemaCommand.CommandText = $"CREATE SCHEMA IF NOT EXISTS {schemaName}";
            await createSchemaCommand.ExecuteNonQueryAsync();
            PostgreSqlObjectsInstaller.Install(migrateConnection, schemaName);

            var jobStorage = new PostgreSqlStorage(
                new TestConnectionFactory(dataSource),
                new PostgreSqlStorageOptions { SchemaName = schemaName });
            var backgroundJobClient = new BackgroundJobClient(jobStorage);
            var monitoringApi = jobStorage.GetMonitoringApi();

            var deletedParentId = backgroundJobClient.Enqueue<ContinuationTracker>(
                HangfireQueues.Default, t => t.FailingJobEndingUpDeleted());
            backgroundJobClient.ContinueJobWith<ContinuationTracker>(
                deletedParentId,
                t => t.RunContinuation(ContinuationTracker.AfterDeletedParent),
                JobContinuationOptions.OnAnyFinishedState);

            var failedParentId = backgroundJobClient.Enqueue<ContinuationTracker>(
                HangfireQueues.Default, t => t.FailingJobEndingUpFailed());
            backgroundJobClient.ContinueJobWith<ContinuationTracker>(
                failedParentId,
                t => t.RunContinuation(ContinuationTracker.AfterFailedParent),
                JobContinuationOptions.OnAnyFinishedState);

            var serverOptions = new BackgroundJobServerOptions
            {
                Queues = [HangfireQueues.Default],
                WorkerCount = 2,
                ServerTimeout = JobTimeout,
                SchedulePollingInterval = TimeSpan.FromSeconds(1),
                Activator = new FixedInstanceJobActivator(tracker)
            };

            using (new BackgroundJobServer(serverOptions, jobStorage))
            {
                Assert.True(
                    tracker.AfterDeletedParentRan.Wait(JobTimeout),
                    "The continuation of the job deleted by OnAttemptsExceeded never ran, so setting " +
                    "OnAttemptsExceeded = Delete does not unblock the publish job. Deleted jobs: " +
                    $"[{string.Join(", ", JobIds(monitoringApi.DeletedJobs(0, 10)))}], failed jobs: " +
                    $"[{string.Join(", ", JobIds(monitoringApi.FailedJobs(0, 10)))}].");

                Assert.Contains(deletedParentId, JobIds(monitoringApi.DeletedJobs(0, 10)));

                await Task.Delay(GracePeriod);

                // Asserting the other parent reached Failed also proves it finished, so the assertion after it is not
                // passing simply because that parent was still running.
                Assert.Contains(failedParentId, JobIds(monitoringApi.FailedJobs(0, 10)));
                Assert.False(
                    tracker.HasRun(ContinuationTracker.AfterFailedParent),
                    "The continuation of a job left in the Failed state ran unexpectedly. Failed no longer blocks " +
                    "continuations, so the OnAttemptsExceeded = Delete workaround may no longer be needed.");
            }
        }
        finally
        {
            try
            {
                try
                {
                    var dropSchemaCommand = migrateConnection.CreateCommand();
                    dropSchemaCommand.CommandText = $"DROP SCHEMA IF EXISTS {schemaName} CASCADE";
                    await dropSchemaCommand.ExecuteNonQueryAsync();
                }
                catch
                {
                    // Ignore cleanup errors
                }

                await migrateConnection.CloseAsync();
            }
            finally
            {
                await migrateConnection.DisposeAsync();
            }
        }
    }

    private static IReadOnlyList<string> JobIds<T>(JobList<T> jobs) => jobs.Select(job => job.Key).ToList();

    internal class ContinuationTracker
    {
        internal const string AfterDeletedParent = "after-deleted-parent";
        internal const string AfterFailedParent = "after-failed-parent";

        private readonly ConcurrentDictionary<string, bool> _continuationsRun = new();

        public ManualResetEventSlim AfterDeletedParentRan { get; } = new(false);

        // A single short-delayed retry keeps the test fast while still exercising the real path: fail, retry, exceed attempts.
        [AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 1 }, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        public void FailingJobEndingUpDeleted()
            => throw new InvalidOperationException("Deliberate failure - this job is expected to end up Deleted");

        // The same job without OnAttemptsExceeded, i.e. the behaviour before the fix: it ends up Failed, which is not final.
        [AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 1 })]
        public void FailingJobEndingUpFailed()
            => throw new InvalidOperationException("Deliberate failure - this job is expected to end up Failed");

        public void RunContinuation(string name)
        {
            _continuationsRun[name] = true;
            if (name == AfterDeletedParent)
            {
                AfterDeletedParentRan.Set();
            }
        }

        public bool HasRun(string name) => _continuationsRun.ContainsKey(name);
    }

    // Only ContinuationTracker jobs are enqueued in this test, so a fixed instance is enough and keeps the test from
    // touching the global JobActivator.Current that other tests rely on.
    internal class FixedInstanceJobActivator(object instance) : JobActivator
    {
        public override object ActivateJob(Type jobType) => instance;
    }

    internal class TestConnectionFactory(NpgsqlDataSource dataSource) : IConnectionFactory
    {
        public NpgsqlConnection GetOrCreateConnection() => dataSource.CreateConnection();
    }
}
