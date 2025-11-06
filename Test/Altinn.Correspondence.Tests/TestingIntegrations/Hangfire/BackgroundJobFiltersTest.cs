using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Tests.Invariants;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Diagnostics;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Hangfire
{
    public class BackgroundJobFiltersTests
    { 
        [Fact]
        public async Task ClientFilter_StampsOriginParameter_WhenAmbientOriginIsSet()
        {
            using var factory = new WebApplicationFactory<Program>();
            using var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
            await WithTestStorage(dataSource, async storage =>
            {
                var client = new BackgroundJobClient(storage);
                BackgroundJobContext.Origin = "migrate";
                var jobId = client.Enqueue(() => Console.WriteLine("test"));
                BackgroundJobContext.Origin = null;
                using var connection = storage.GetConnection();
                var origin = connection.GetJobParameter(jobId, "Origin");
                Assert.Equal("migrate", origin?.Trim('"'));
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ServerAndClientFilters_PropagateOrigin_ToChildJobs()
        {
            using var factory = new WebApplicationFactory<Program>();
            using var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();

            await WithTestStorage(dataSource, async storage =>
            {
                var client = new BackgroundJobClient(storage);

                BackgroundJobContext.Origin = "migrate";
                var parentId = client.Enqueue<PropagationJobs>(x => x.ParentEnqueueChild());
                BackgroundJobContext.Origin = null;
                PropagationJobs.LastChildJobId = null;
                
                using var server = new BackgroundJobServer(new BackgroundJobServerOptions
                {
                    Queues = [HangfireQueues.Default],
                    WorkerCount = 1,
                    SchedulePollingInterval = TimeSpan.FromMilliseconds(250)
                }, storage);

                // wait up to 10s for child job id to be recorded
                var sw = Stopwatch.StartNew();
                string? childId = null;
                while (sw.Elapsed < TimeSpan.FromSeconds(10))
                {
                    childId = PropagationJobs.LastChildJobId; 
                    if (!string.IsNullOrEmpty(childId)) break;
                    await Task.Delay(100);
                }
                Assert.False(string.IsNullOrEmpty(childId));

                using var connection = storage.GetConnection();
                var origin = connection.GetJobParameter(childId!, "Origin");
                Assert.Equal("migrate", origin?.Trim('"'));
            });
        }

        private static async Task WithTestStorage(NpgsqlDataSource dataSource, Func<PostgreSqlStorage, Task> run)
        {
            var schema = $"hangfire_test_{Guid.NewGuid().ToString("N")[..8]}";
            await using var conn = await dataSource.OpenConnectionAsync();
            var prevStorage = JobStorage.Current;
            var prevFilters = GlobalJobFilters.Filters.ToList();
            try
            {
                PostgreSqlObjectsInstaller.Install(conn, schema);
                var storage = new PostgreSqlStorage(new HangfireStorageCompatibilityTests.TestConnectionFactory(dataSource), new PostgreSqlStorageOptions { SchemaName = schema });

                JobStorage.Current = storage;
                GlobalJobFilters.Filters.Clear();
                GlobalJobFilters.Filters.Add(new BackgroundJobClientFilter());
                GlobalJobFilters.Filters.Add(new BackgroundJobServerFilter());

                await run(storage);
            }
            finally
            {
                // Restore globals
                GlobalJobFilters.Filters.Clear();
                foreach (var filter in prevFilters)
                {
                    GlobalJobFilters.Filters.Add(filter.Instance, filter.Order);
                }
                JobStorage.Current = prevStorage;

                try
                {
                    var drop = conn.CreateCommand();
                    drop.CommandText = $"DROP SCHEMA IF EXISTS {schema} CASCADE";
                    await drop.ExecuteNonQueryAsync();
                }
                catch { }
            }
        }

        public class PropagationJobs
        {
            public static string? LastChildJobId;

            public void ParentEnqueueChild()
            {
                var childId = BackgroundJob.Enqueue(() => Console.WriteLine("Hello World!"));
                LastChildJobId = childId;
            }
        }
    }
}