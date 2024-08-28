using Hangfire.PostgreSql;
using Npgsql;

namespace Altinn.Correspondence.Integrations.Hangfire;

public class HangfireConnectionFactory(NpgsqlDataSource dataSource) : IConnectionFactory
{
    public NpgsqlConnection GetOrCreateConnection() => dataSource.CreateConnection();
}
