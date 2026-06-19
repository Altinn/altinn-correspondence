using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Testcontainers.PostgreSql;

namespace Altinn.Correspondence.Tests.Fixtures;

public class PostgresTestcontainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private DbContextOptions<ApplicationDbContext> _dbContextOptions;

    public PostgresTestcontainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .WithPassword("postgres")
            .WithUsername("postgres")
            .WithDatabase("correspondence")
            .WithPortBinding(5432, true)
            .Build();
    }

    public TestApplicationDbContext CreateDbContext()
    {
        return new TestApplicationDbContext(_dbContextOptions);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_container.GetConnectionString(), x =>
            {
                x.MigrationsAssembly("Altinn.Correspondence.Persistence");
                x.ExecutionStrategy(dependencies => new NonRetryingExecutionStrategy(dependencies));
            })
            .Options;

        // Apply migrations
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        await using var context = new TestApplicationDbContext(_dbContextOptions);
        await context.Database.EnsureCreatedAsync();
        if ((await context.Database.GetPendingMigrationsAsync()).Any()) 
        { 
            await context.Database.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
