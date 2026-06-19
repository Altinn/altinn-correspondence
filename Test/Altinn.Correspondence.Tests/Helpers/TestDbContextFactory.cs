using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test", npgsql =>
                npgsql.ExecutionStrategy(dependencies =>
                    new CorrespondenceNpgsqlRetryingExecutionStrategy(dependencies)))
            .Options;
        return new ApplicationDbContext(options);
    }
}
