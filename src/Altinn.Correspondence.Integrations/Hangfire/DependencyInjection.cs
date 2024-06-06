using Altinn.Correspondence.Persistence;

using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Integrations.Hangfire;
public static class DependencyInjection
{
    public static void ConfigureHangfire(this IServiceCollection services, string connectionString)
    {
        var serviceProvider = services.BuildServiceProvider();
        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(
                c => c.UseNpgsqlConnection(connectionString)
            )
        );
        services.AddHangfireServer();
    }
}
