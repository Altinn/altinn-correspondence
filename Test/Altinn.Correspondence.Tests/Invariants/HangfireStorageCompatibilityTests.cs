using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Transactions;

namespace Altinn.Correspondence.Tests.Invariants;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class HangfireStorageCompatibilityTests
{
    // TransactionScope is used to ensure eventual consistency for scheduled background jobs (as used for outbox pattern for posts to events API) 
    // Test ensures that Hangfire implementation is compatible with TransactionScope.

    [Fact]
    public async Task BackgroundJobClient_TransactionScopeCompatible()
    {
        using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) => { });
        using var testDataSource = testFactory.Services.GetRequiredService<NpgsqlDataSource>();

        var migrateConnection = await testDataSource.OpenConnectionAsync();
        try
        {
            PostgreSqlObjectsInstaller.Install(migrateConnection);
            var connectionFactory = new TestConnectionFactory(testDataSource);
            var jobStorage = new PostgreSqlStorage(connectionFactory);
            var backgroundJobClient = new BackgroundJobClient(jobStorage);
            long parentJobId;
            var outsideConnection = await testDataSource.OpenConnectionAsync();
            using (var transaction = new TransactionScope(TransactionScopeOption.Required))
            {
                var parentJob = backgroundJobClient.Enqueue(() => Console.WriteLine("Hello World!"));
                parentJobId = long.Parse(parentJob);
                var command = outsideConnection.CreateCommand();
                command.CommandText = "select COUNT(job) FROM hangfire.job WHERE id = @jobId";
                command.Parameters.AddWithValue("jobId", parentJobId);
                var result = command.ExecuteScalar();
                Assert.Equal(0, (long?)command.ExecuteScalar());
                transaction.Complete();
            }
            var postCommitCommand = testDataSource.CreateCommand("select COUNT(job) FROM hangfire.job WHERE id = @jobId");
            postCommitCommand.Parameters.AddWithValue("jobId", parentJobId);
            Assert.Equal(1, (long?)postCommitCommand.ExecuteScalar());
        }
        finally
        {
            try
            {
                await migrateConnection.CloseAsync();
            }
            finally
            {
                migrateConnection.Dispose();
            }
        }
    }

    /*
     * As Hangfire spec does not have strictly specified execution order for the queues, this invariant verifies that we can assume an ordering by queue from our Hangfire implementation.
     * Used for the migration A3->Dialogporten
     * */
    [Fact]
    public async Task BackgroundJobClient_PicksByQueue()
    {
        int jobsCount = 10; // Total number of jobs to enqueue
        var testJobTracker = new TestJobTracker(jobsCount);
        using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) => { // Only used to get the database connection
            services.AddSingleton<TestJobTracker>(testJobTracker);
        });
        using var testDataSource = testFactory.Services.GetRequiredService<NpgsqlDataSource>();
        var migrateConnection = await testDataSource.OpenConnectionAsync();

        // Create unique schema name for test isolation
        var testId = Guid.NewGuid().ToString("N")[..8];
        var schemaName = $"hangfire_test_{testId}";

        try
        {
            // Create a unique schema for this test
            var createSchemaCommand = migrateConnection.CreateCommand();
            createSchemaCommand.CommandText = $"CREATE SCHEMA IF NOT EXISTS {schemaName}";
            await createSchemaCommand.ExecuteNonQueryAsync();

            // Initialize Hangfire schema in the test-specific schema
            PostgreSqlObjectsInstaller.Install(migrateConnection, schemaName);

            var connectionFactory = new TestConnectionFactory(testDataSource);
            var jobStorage = new PostgreSqlStorage(connectionFactory, new PostgreSqlStorageOptions
            {
                SchemaName = schemaName
            });
            var backgroundJobClient = new BackgroundJobClient(jobStorage);

            // Enqueue jobs in different queues but added in random order
            var randomGen = new Random();
            var migrationJobs = new List<string>();
            var defaultJobs = new List<string>();
            for (int i = 1; i <= jobsCount; i++)
            {
                bool scheduleAsMigrationJob = randomGen.Next(2) == 0;
                if (scheduleAsMigrationJob)
                {
                    backgroundJobClient.Enqueue<TestJobTracker>("migration", (testJobTracker) => testJobTracker.ExecuteJob("migration-job-" + i));
                    migrationJobs.Add(i.ToString());
                } else
                {
                    backgroundJobClient.Enqueue<TestJobTracker>((testJobTracker) => testJobTracker.ExecuteJob("default-job-" + i));
                    defaultJobs.Add(i.ToString());
                }
            }

            // Verify jobs are in database and correct queues using monitoring API
            var monitoringApi = jobStorage.GetMonitoringApi();

            // Check jobs in default queue
            var defaultQueueJobs = monitoringApi.EnqueuedJobs("default", 0, jobsCount);
            Assert.Equal(defaultJobs.Count, defaultQueueJobs.Count);

            // Check jobs in migration queue
            var migrationQueueJobs = monitoringApi.EnqueuedJobs("migration", 0, jobsCount);
            Assert.Equal(migrationJobs.Count, migrationQueueJobs.Count);

            // Verify the specific job IDs are in the correct queues
            var actualDefaultJobIds = defaultQueueJobs.Select(j => j.Key).ToArray();
            var actualMigrationJobIds = migrationQueueJobs.Select(j => j.Key).ToArray();

            Assert.All(actualDefaultJobIds, jobId =>
                Assert.Contains(jobId, defaultJobs));
            Assert.All(actualMigrationJobIds, jobId =>
                Assert.Contains(jobId, migrationJobs));

            // Start background job server with queue priority
            var serverOptions = new BackgroundJobServerOptions
            {
                Queues = new[] { "default", "migration" }, // default processed first
                WorkerCount = 1, // Single worker for deterministic ordering
                ServerTimeout = TimeSpan.FromSeconds(30),
                SchedulePollingInterval = TimeSpan.FromSeconds(1)
            };

            using var server = new BackgroundJobServer(serverOptions, jobStorage);

            // Wait for all jobs to complete
            var allCompleted = testJobTracker.Countdown.Wait(TimeSpan.FromSeconds(30));

            Assert.True(allCompleted,
                $"Not all jobs completed. Executed: {testJobTracker.GetExecutionCount()}/4. " +
                $"Order: [{string.Join(", ", testJobTracker.GetExecutionOrder())}]");

            // Verify execution order
            var executionList = testJobTracker.GetExecutionOrder();

            var defaultIndices = executionList
                .Select((job, index) => new { job, index })
                .Where(x => x.job.StartsWith("default"))
                .Select(x => x.index)
                .ToList();

            var migrationIndices = executionList
                .Select((job, index) => new { job, index })
                .Where(x => x.job.StartsWith("migration"))
                .Select(x => x.index)
                .ToList();

            Assert.Equal(defaultQueueJobs.Count + migrationQueueJobs.Count, executionList.Count);
            var maxDefaultIndex = defaultIndices.Max();
            var minMigrationIndex = migrationIndices.Min();

            Assert.True(maxDefaultIndex < minMigrationIndex,
                $"Default queue jobs should execute before migration queue jobs. " +
                $"Execution order: [{string.Join(", ", executionList)}]");
        }
        finally
        {
            try
            {
                // Clean up test schema
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
                migrateConnection.Dispose();
            }
        }

    }

    [Fact]
    public void Startup_ConfiguresHangfireQueuesCorrectly()
    {
        var testId = Guid.NewGuid().ToString("N")[..8];
        var schemaName = $"hangfire_test_{testId}";

        using var testFactory = new WebApplicationFactory<Program>();

        // Get the Hangfire server hosted service to inspect its configuration
        var hostedServices = testFactory.Services.GetServices<IHostedService>();
        var hangfireServer = hostedServices.FirstOrDefault(s => s.GetType().Name.Contains("BackgroundJobServer"));

        Assert.NotNull(hangfireServer);

        // Use reflection to get the server options from the Hangfire server
        var serverType = hangfireServer.GetType();
        var optionsField = serverType.GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance);

        if (optionsField != null)
        {
            var serverOptions = (BackgroundJobServerOptions)optionsField.GetValue(hangfireServer);

            Assert.NotNull(serverOptions);
            Assert.Equal(new[] { "default", "migration" }, serverOptions.Queues);
            Assert.Equal("default", serverOptions.Queues[0]); // Should be highest priority
        }
        else
        {
            // Alternative approach: check if options are accessible via properties
            var optionsProperty = serverType.GetProperty("Options", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (optionsProperty != null)
            {
                var serverOptions = (BackgroundJobServerOptions)optionsProperty.GetValue(hangfireServer);

                Assert.NotNull(serverOptions);
                Assert.Equal(new[] { "default", "migration" }, serverOptions.Queues);
                Assert.Equal(TimeSpan.FromSeconds(2), serverOptions.SchedulePollingInterval);
            }
            else
            {
                Assert.True(false, "Could not access BackgroundJobServerOptions from the Hangfire server instance");
            }
        }
    }
    internal class TestJobTracker
    {
        private readonly ConcurrentQueue<string> ExecutionOrder = new();
        public CountdownEvent Countdown;

        public TestJobTracker(int jobCount)
        {
            Countdown = new CountdownEvent(jobCount);
        }

        public void ExecuteJob(string jobName)
        {
            Thread.Sleep(200); // Simulate work
            ExecutionOrder.Enqueue(jobName);
            Countdown.Signal();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Executed job #{GetExecutionCount()}: {jobName}");
        }

        public List<string> GetExecutionOrder() => ExecutionOrder.ToList();
        public int GetExecutionCount() => ExecutionOrder.Count;
    }

    internal class TestConnectionFactory(NpgsqlDataSource dataSource) : IConnectionFactory
    {
        public NpgsqlConnection GetOrCreateConnection()
        {
            return dataSource.CreateConnection();
        }
    }
}
