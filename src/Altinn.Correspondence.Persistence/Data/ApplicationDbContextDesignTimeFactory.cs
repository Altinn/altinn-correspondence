using Altinn.Correspondence.Core.Options;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Altinn.Correspondence.Persistence;

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
            throw new InvalidOperationException($"Connection string not found for environment {environment}.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(databaseOptions.ConnectionString);

        if (!string.IsNullOrWhiteSpace(connectionStringBuilder.Password))
        {
            Console.WriteLine("Factory: Using database connection with password");
            optionsBuilder.UseNpgsql(databaseOptions.ConnectionString);
        }
        else
        {
            Console.WriteLine("Factory: Using database connection with token - acquiring token synchronously");

            try
            {
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" });

                // Get token synchronously
                var token = credential.GetTokenAsync(tokenRequestContext, CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine($"Factory: Token acquired successfully, expires: {token.ExpiresOn}");

                // Create connection string with token
                connectionStringBuilder.Password = token.Token;
                var authenticatedConnectionString = connectionStringBuilder.ConnectionString;

                // Use the connection string directly with UseNpgsql, NOT with NpgsqlDataSource
                optionsBuilder.UseNpgsql(authenticatedConnectionString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Factory: Error acquiring token: {ex.Message}");
                throw;
            }
        }

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
