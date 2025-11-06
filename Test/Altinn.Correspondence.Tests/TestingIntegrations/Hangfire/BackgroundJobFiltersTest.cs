using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Altinn.Correspondence.Tests.Helpers;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Hangfire
{
    public class BackgroundJobFiltersTests
    {
        [Fact]
        public void ClientFilter_StampsOriginParameter_WhenAmbientOriginIsSet()
        {
            using var factory = new UnitWebApplicationFactory(_ => { });
            var storage = factory.Services.GetRequiredService<JobStorage>();
            var client = factory.Services.GetRequiredService<IBackgroundJobClient>();
            BackgroundJobContext.Origin = "migrate";
            var jobId = client.Enqueue(() => Console.WriteLine("test"));
            BackgroundJobContext.Origin = null;
            using var connection = storage.GetConnection();
            var origin = connection.GetJobParameter(jobId, "Origin");
            Assert.Equal("migrate", origin?.Trim('"'));
        }

        [Fact]
        public async Task ServerAndClientFilters_PropagateOrigin_ToChildJobs()
        {
            using var factory = new UnitWebApplicationFactory(_ => { });
            var storage = factory.Services.GetRequiredService<JobStorage>();
            var client = factory.Services.GetRequiredService<IBackgroundJobClient>();

            BackgroundJobContext.Origin = "migrate";
            var parentId = client.Enqueue<PropagationJobs>(x => x.ParentEnqueueChild());
            BackgroundJobContext.Origin = null;
            PropagationJobs.LastChildJobId = null;

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