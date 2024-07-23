using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Hangfire.MemoryStorage;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    internal Mock<IBackgroundJobClient>? HangfireBackgroundJobClient;
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        // Overwrite registrations from Program.cs
        builder.ConfigureTestServices((services) =>
        {
            services.AddHangfire(config =>
                           config.UseMemoryStorage()
                       );
            HangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            services.AddSingleton(HangfireBackgroundJobClient.Object);
            var altinnAuthorizationService = new Mock<IAltinnAuthorizationService>();
            altinnAuthorizationService.Setup(x => x.CheckUserAccess(It.IsAny<string>(), It.IsAny<List<ResourceAccessLevel>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            services.AddSingleton(altinnAuthorizationService.Object);
        });

    }

    public HttpClient CreateClientInternal()
    {
        var client = CreateClient();
        return client;
    }
}
