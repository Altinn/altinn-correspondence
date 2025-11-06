using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Tests.Invariants;
using Hangfire;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Altinn.Correspondence.Tests.Helpers;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Hangfire
{
    public class BackgroundJobFiltersTests
    { 
        [Fact]
        public async Task ClientFilter_StampsOriginParameter_WhenAmbientOriginIsSet()
        {
            using var factory = new UnitWebApplicationFactory(_ => { });
            await WithAppStorage(factory, async storage =>
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
            using var factory = new UnitWebApplicationFactory(_ => { });
            await WithAppStorage(factory, async storage =>
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

        private static async Task WithAppStorage(WebApplicationFactory<Program> factory, Func<JobStorage, Task> run)
        {
            var prevStorage = JobStorage.Current;
            var prevFilters = GlobalJobFilters.Filters.ToList();
            try
            {
                var storage = factory.Services.GetRequiredService<JobStorage>();

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
            }
        }

        public class PropagationJobs
        {
            public static volatile string? LastChildJobId;

            public void ParentEnqueueChild()
            {
                var childId = BackgroundJob.Enqueue(() => Console.WriteLine("Hello World!"));
                LastChildJobId = childId;
            }
        }
    }
}