using Altinn.Correspondence.Core.Options;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;
namespace Altinn.Correspondence.Persistence.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var databaseOptions = new DatabaseOptions() { ConnectionString = "" };
        configuration.GetSection(nameof(DatabaseOptions)).Bind(databaseOptions);

        if (string.IsNullOrEmpty(databaseOptions.ConnectionString))
        {
            throw new InvalidOperationException($"Connection string 'DatabaseOptions:ConnectionString' not found for environment {environment}.");
        }

        Console.WriteLine($"Using environment: {environment}");
        Console.WriteLine($"Connection string: {databaseOptions.ConnectionString}");

        // Use the same NpgsqlDataSource approach as your DI
        var dataSource = BuildAzureNpgsqlDataSource(databaseOptions.ConnectionString);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(dataSource);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static NpgsqlDataSource BuildAzureNpgsqlDataSource(string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder();
        dataSourceBuilder.ConnectionStringBuilder.ConnectionString = connectionString;

        if (!string.IsNullOrWhiteSpace(dataSourceBuilder.ConnectionStringBuilder.Password))
        {
            Console.WriteLine("Using database connection with password (local development)");
            return dataSourceBuilder.Build();
        }

        Console.WriteLine("Using database connection with token (remote)");
        var psqlServerTokenProvider = new DefaultAzureCredential();
        var tokenRequestContext = new Azure.Core.TokenRequestContext(scopes: ["https://ossrdbms-aad.database.windows.net/.default"]) { };
        dataSourceBuilder.UsePeriodicPasswordProvider(async (_, cancellationToken) =>
            psqlServerTokenProvider.GetTokenAsync(tokenRequestContext).Result.Token,
            TimeSpan.FromMinutes(45),
            TimeSpan.FromSeconds(0)
        );
        return dataSourceBuilder.Build();
    }
}
