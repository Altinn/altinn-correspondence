using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Slack.Webhooks;
using Altinn.Correspondence.Integrations.Hangfire; // Add this line

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
            var sp = services.BuildServiceProvider();
            config.UseFilter(sp.GetRequiredService<SlackExceptionHandler>());
        });
        services.AddHangfireServer(options => options.SchedulePollingInterval = TimeSpan.FromSeconds(2));
    }
}
