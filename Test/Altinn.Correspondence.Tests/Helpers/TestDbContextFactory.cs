using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Altinn.Correspondence.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create(int maxRetryCount = 0)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test", npgsql =>
            {
                if (maxRetryCount == 0)
                {
                    npgsql.ExecutionStrategy(dependencies => new NonRetryingExecutionStrategy(dependencies));
                }
                else
                {
                    npgsql.ExecutionStrategy(dependencies =>
                        new CorrespondenceNpgsqlRetryingExecutionStrategy(dependencies, maxRetryCount));
                }
            })
            .Options;
        return new ApplicationDbContext(options);
    }
}
