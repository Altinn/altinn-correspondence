using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Altinn.Correspondence.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Altinn.Correspondence.Tests.Helpers;

public static class TestDbContextFactory
{
    // Matches docker-compose.yml and appsettings.Development.json
    private const string ConnectionString =
        "Host=localhost:5432;Username=postgres;Password=postgres;Database=correspondence;Maximum Pool Size=50;Timeout=30";

    private static readonly Lazy<DbContextOptions<ApplicationDbContext>> Options = new(BuildOptions);

    public static TestApplicationDbContext Create() => new(Options.Value);

    public static TestApplicationDbContext Create(int maxRetryCount)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("Altinn.Correspondence.Persistence");
                ConfigureExecutionStrategy(npgsql, maxRetryCount);
            })
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static DbContextOptions<ApplicationDbContext> BuildOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("Altinn.Correspondence.Persistence");
                ConfigureExecutionStrategy(npgsql, maxRetryCount: 0);
            })
            .Options;
    }

    private static void ConfigureExecutionStrategy(NpgsqlDbContextOptionsBuilder npgsql, int maxRetryCount)
    {
        if (maxRetryCount == 0)
        {
            npgsql.ExecutionStrategy(dependencies => new NonRetryingExecutionStrategy(dependencies));
        }
        else
        {
            npgsql.ExecutionStrategy(dependencies =>
                new CorrespondenceNpgsqlRetryingExecutionStrategy(dependencies));
        }
    }
}
