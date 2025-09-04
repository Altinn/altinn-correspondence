using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Persistence.Helpers;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Polly;
using System;
using System.IdentityModel.Tokens.Jwt;

namespace Altinn.Correspondence.Persistence;

public class ApplicationDbContext : DbContext
{
    public string? _accessToken;
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
        var conn = this.Database.GetDbConnection();
        if (IsAccessTokenValid())
        {
            var connectionString = conn.ConnectionString;
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
            var token = credential
                .GetToken(
                    new Azure.Core.TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" })
                );
            _accessToken = token.Token;
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            connectionStringBuilder.Password = token.Token;
            conn.ConnectionString = connectionStringBuilder.ToString();
        }
    }

    public DbSet<AttachmentEntity> Attachments { get; set; }
    public DbSet<AttachmentStatusEntity> AttachmentStatuses { get; set; }
    public DbSet<CorrespondenceAttachmentEntity> CorrespondenceAttachments { get; set; }
    public DbSet<CorrespondenceContentEntity> CorrespondenceContents { get; set; }
    public DbSet<CorrespondenceEntity> Correspondences { get; set; }
    public DbSet<CorrespondenceStatusEntity> CorrespondenceStatuses { get; set; }
    public DbSet<CorrespondenceForwardingEventEntity> CorrespondenceForwardingEvents { get; set; }
    public DbSet<CorrespondenceNotificationEntity> CorrespondenceNotifications { get; set; }
    public DbSet<CorrespondenceReplyOptionEntity> CorrespondenceReplyOptions { get; set; }
    public DbSet<ExternalReferenceEntity> ExternalReferences { get; set; }
    public DbSet<NotificationTemplateEntity> NotificationTemplates { get; set; }
    public DbSet<LegacyPartyEntity> LegacyParties { get; set; }
    public DbSet<IdempotencyKeyEntity> IdempotencyKeys { get; set; } = null!;
    public DbSet<ServiceOwnerEntity> ServiceOwners { get; set; }
    public DbSet<StorageProviderEntity> StorageProviders { get; set; }

    private bool IsAccessTokenValid()
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            return false;
        }
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken token = tokenHandler.ReadToken(_accessToken);
        return DateTimeOffset.UtcNow.AddSeconds(60) < token.ValidTo;
    }

    public async Task EnsureTokenAsync()
    {
        if (IsAccessTokenValid())
            return;

        var conn = this.Database.GetDbConnection();
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
        var token = await credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" })
        );

        _accessToken = token.Token;

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(conn.ConnectionString);
        connectionStringBuilder.Password = token.Token;
        conn.ConnectionString = connectionStringBuilder.ToString();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("correspondence");
    }
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConverter>();
    }
}

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
        Console.WriteLine("Design time factory");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(databaseOptions.ConnectionString);
        if (connectionStringBuilder.Host == "localhost")
        {
            optionsBuilder.UseNpgsql(databaseOptions.ConnectionString);
        } 
        else 
        { 
            optionsBuilder.UseNpgsql(databaseOptions.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.ConfigureDataSource(dataSourceBuilder =>
                {
                    dataSourceBuilder.UsePeriodicPasswordProvider(async (settings, ct) =>
                    {
                        var credential = new DefaultAzureCredential();
                        var tokenRequestContext = new TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" });
                        var token = await credential.GetTokenAsync(tokenRequestContext, ct);
                        return token.Token;
                    }, TimeSpan.FromHours(1), TimeSpan.FromMinutes(55));
                });
            });
        }

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}