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
        });

    }

    public HttpClient CreateClientInternal()
    {
        var client = CreateClient();
        return client;
    }
}
