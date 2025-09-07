using Altinn.Correspondence.Core.Options;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Altinn.Correspondence.Persistence.Data;

/**
 * This is used by the migrations bundle
 * */
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
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(databaseOptions.ConnectionString);
        if (!string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
        {
            Console.WriteLine("Authenticating with password");
            optionsBuilder.UseNpgsql(databaseOptions.ConnectionString);
        } 
        else
        {
            Console.WriteLine("Authenticating with token");
            var sourceBuilder = new NpgsqlDataSourceBuilder(databaseOptions.ConnectionString);
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(scopes: ["https://ossrdbms-aad.database.windows.net/.default"]) { };
            sourceBuilder.UsePeriodicPasswordProvider(async (_, cancellationToken) =>
                credential.GetTokenAsync(tokenRequestContext).Result.Token, TimeSpan.FromMinutes(45), TimeSpan.FromSeconds(0)
            );
            var dataSource = sourceBuilder.Build();
            optionsBuilder.UseNpgsql(dataSource);
        }

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
