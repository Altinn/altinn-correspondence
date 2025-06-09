using Altinn.Correspondence.Core.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Altinn.Correspondence.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var databaseOptions = new DatabaseOptions { ConnectionString = string.Empty };
        configuration.GetSection(nameof(DatabaseOptions)).Bind(databaseOptions);
        
        if (string.IsNullOrEmpty(databaseOptions.ConnectionString))
        {
            throw new InvalidOperationException("Database connection string not found in configuration.");
        }

        optionsBuilder.UseNpgsql(databaseOptions.ConnectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
} 