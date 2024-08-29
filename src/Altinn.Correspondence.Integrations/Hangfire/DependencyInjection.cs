using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Integrations.Hangfire;
public static class DependencyInjection
{
    public static void ConfigureHangfire(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionFactory, HangfireConnectionFactory>();
        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(
                c => c.UseConnectionFactory(services.BuildServiceProvider().GetService<IConnectionFactory>())
            )
        );
        services.AddHangfireServer();
    }
}
