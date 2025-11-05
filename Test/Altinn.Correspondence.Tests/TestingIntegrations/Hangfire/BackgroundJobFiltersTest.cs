using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Tests.Invariants;
using Altinn.Correspondence.Tests.Helpers;
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
            var schema = $"hangfire_test_{Guid.NewGuid().ToString("N")[..8]}";

            var conn = await dataSource.OpenConnectionAsync();
            try
            {
                PostgreSqlObjectsInstaller.Install(conn, schema);
                var storage = new PostgreSqlStorage(new HangfireStorageCompatibilityTests.TestConnectionFactory(dataSource), new PostgreSqlStorageOptions { SchemaName = schema });
                JobStorage.Current = storage;

                // Register filters for this storage scope
                GlobalJobFilters.Filters.Clear();
                GlobalJobFilters.Filters.Add(new BackgroundJobClientFilter());
                GlobalJobFilters.Filters.Add(new BackgroundJobServerFilter());

                var client = new BackgroundJobClient(storage);

                BackgroundJobContext.Origin = "migrate";
                var jobId = client.Enqueue(() => Console.WriteLine("test"));
                BackgroundJobContext.Origin = null; // cleanup

                using var connection = storage.GetConnection();
                var origin = connection.GetJobParameter(jobId, "Origin");
                Assert.Equal("migrate", origin?.Trim('"'));
            }
            finally
            {
                var drop = conn.CreateCommand();
                drop.CommandText = $"DROP SCHEMA IF EXISTS {schema} CASCADE";
                await drop.ExecuteNonQueryAsync();
                try { await conn.CloseAsync(); } finally { conn.Dispose(); }
            }
        }

        [Fact]
        public async Task ServerAndClientFilters_PropagateOrigin_ToChildJobs()
        {
            using var factory = new WebApplicationFactory<Program>();
            using var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();

            var conn = await dataSource.OpenConnectionAsync();
            string schema = $"hangfire_test_{Guid.NewGuid().ToString("N")[..8]}";
            try
            {
                PostgreSqlObjectsInstaller.Install(conn, schema);
                var storage = new PostgreSqlStorage(new HangfireStorageCompatibilityTests.TestConnectionFactory(dataSource), new PostgreSqlStorageOptions { SchemaName = schema });
                JobStorage.Current = storage;

                // Register filters for this storage scope
                GlobalJobFilters.Filters.Clear();
                GlobalJobFilters.Filters.Add(new BackgroundJobClientFilter());
                GlobalJobFilters.Filters.Add(new BackgroundJobServerFilter());

                var client = new BackgroundJobClient(storage);

                BackgroundJobContext.Origin = "migrate";
                var parentId = client.Enqueue<PropagationJobs>(x => x.ParentEnqueueChild());
                BackgroundJobContext.Origin = null;

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
            }
            finally
            {
                JobStorage.Current = null;
                try
                {
                    var drop = conn.CreateCommand();
                    drop.CommandText = $"DROP SCHEMA IF EXISTS {schema} CASCADE";
                    await drop.ExecuteNonQueryAsync();
                }
                catch { }
                try { await conn.CloseAsync(); } finally { conn.Dispose(); }
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