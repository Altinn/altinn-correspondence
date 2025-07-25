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

        // Check if we need to add a token
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(databaseOptions.ConnectionString);
        if (string.IsNullOrWhiteSpace(connectionStringBuilder.Password))
        {
            Console.WriteLine("No password found, acquiring Azure AD token...");
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
            var token = credential.GetToken(
                new Azure.Core.TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" })
            );
            connectionStringBuilder.Password = token.Token;
            Console.WriteLine("Token acquired successfully");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionStringBuilder.ConnectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
