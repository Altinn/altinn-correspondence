using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
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
    }
}