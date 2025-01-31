using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Slack.Webhooks;
using Altinn.Correspondence.Integrations.Slack; // Add this line
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Integrations.Hangfire;
public static class DependencyInjection
{
    public static void ConfigureHangfire(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionFactory, HangfireConnectionFactory>();
        services.AddHangfire(config =>
        {
            config.UsePostgreSqlStorage(
                c => c.UseConnectionFactory(services.BuildServiceProvider().GetService<IConnectionFactory>())
            );
            config.UseSerializerSettings(new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            config.UseFilter(new SlackExceptionHandler(services.BuildServiceProvider().GetService<SlackExceptionNotification>(), 
                                services.BuildServiceProvider().GetService<ILogger<SlackExceptionHandler>>()));
        });
        services.AddHangfireServer(options => options.SchedulePollingInterval = TimeSpan.FromSeconds(2));
    }
}
