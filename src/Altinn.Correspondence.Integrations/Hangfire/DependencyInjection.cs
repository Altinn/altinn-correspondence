using Altinn.Correspondence.Integrations.Slack;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Altinn.Correspondence.Integrations.Hangfire;
public static class DependencyInjection
{
    public static void ConfigureHangfire(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionFactory, HangfireConnectionFactory>();
        services.AddHangfire((provider, config) =>
        {
            config.UsePostgreSqlStorage(
                c => c.UseConnectionFactory(provider.GetService<IConnectionFactory>())
            );
            config.UseSerilogLogProvider();
            config.UseFilter(new HangfireAppRequestFilter(provider.GetRequiredService<TelemetryClient>()));
            config.UseSerializerSettings(new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            config.UseFilter(new SlackExceptionHandler(services.BuildServiceProvider().GetService<SlackExceptionNotification>(), 
                                services.BuildServiceProvider().GetService<ILogger<SlackExceptionHandler>>()));
        });
        services.AddHangfireServer(options => options.SchedulePollingInterval = TimeSpan.FromSeconds(2));
    }
}
