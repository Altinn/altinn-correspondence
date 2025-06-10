using Altinn.Correspondence.Integrations.Slack;
using Hangfire;
using Hangfire.AspNetCore;
using Hangfire.PostgreSql;
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
            config.UseLogProvider(new AspNetCoreLogProvider(provider.GetRequiredService<ILoggerFactory>()));
            config.UseFilter(new HangfireAppRequestFilter());
            config.UseSerializerSettings(new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            config.UseFilter(
                new SlackExceptionHandler(
                    provider.GetRequiredService<SlackExceptionNotificationHandler>(),
                    provider.GetRequiredService<ILogger<SlackExceptionHandler>>())
                );
        });

        services.AddHangfireServer(options => options.SchedulePollingInterval = TimeSpan.FromSeconds(2));
    }
}
