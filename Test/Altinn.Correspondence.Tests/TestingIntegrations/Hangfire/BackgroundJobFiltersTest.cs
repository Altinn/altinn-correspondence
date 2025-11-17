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
            OriginProbe.LastObservedOrigin = null;

            // Wait for child job to capture context
            var wait = Stopwatch.StartNew();
            while (wait.Elapsed < TimeSpan.FromSeconds(30) && OriginProbe.LastObservedOrigin == null)
            {
                await Task.Delay(100);
            }
            Assert.Equal("migrate", OriginProbe.LastObservedOrigin);
        }

        public class PropagationJobs(IBackgroundJobClient client)
        {
            private readonly IBackgroundJobClient _client = client;

            public void ParentEnqueueChild()
            {
                var childId = _client.Enqueue(() => OriginProbe.Capture());
            }
        }

        public static class OriginProbe
        {
            public static volatile string? LastObservedOrigin;
            public static void Capture()
            {
                LastObservedOrigin = BackgroundJobContext.Origin;
            }
        }
    }
}