using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Repositories;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Altinn.Correspondence.Persistence;
public static class DependencyInjection
{
    public static void AddPersistence(this IServiceCollection services, IConfiguration config, ILogger bootstrapLogger)
    {
        services.AddSingleton(BuildAzureNpgsqlDataSource(config, bootstrapLogger));
        services.AddDbContext<ApplicationDbContext>(entityFrameworkConfig =>
        {
            entityFrameworkConfig.UseNpgsql();
        });
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IAttachmentStatusRepository, AttachmentStatusRepository>();
        services.AddScoped<ICorrespondenceRepository, CorrespondenceRepository>();
        services.AddScoped<ICorrespondenceStatusRepository, CorrespondenceStatusRepository>();
        services.AddScoped<ICorrespondenceNotificationRepository, CorrespondenceNotificationRepository>();
        services.AddScoped<ICorrespondenceForwardingEventRepository, CorrespondenceForwardingEventRepository>();
        services.AddSingleton<IStorageRepository, StorageRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<ILegacyPartyRepository, LegacyPartyRepository>();
        services.AddScoped<IIdempotencyKeyRepository, IdempotencyKeyRepository>();
        services.AddScoped<IServiceOwnerRepository, ServiceOwnerRepository>();
    }

    private static NpgsqlDataSource BuildAzureNpgsqlDataSource(IConfiguration config, ILogger bootstrapLogger)
    {
        var databaseOptions = new DatabaseOptions() { ConnectionString = "" };
        config.GetSection(nameof(DatabaseOptions)).Bind(databaseOptions);
        var dataSourceBuilder = new NpgsqlDataSourceBuilder();
        dataSourceBuilder.ConnectionStringBuilder.ConnectionString = databaseOptions.ConnectionString;
        if (!string.IsNullOrWhiteSpace(dataSourceBuilder.ConnectionStringBuilder.Password))
        {
            bootstrapLogger.LogInformation("Using database connection with password (local development/migration)");
            return dataSourceBuilder.Build();
        }

        bootstrapLogger.LogInformation("Using database connection with token (remote)");
        var psqlServerTokenProvider = new DefaultAzureCredential();
        var tokenRequestContext = new TokenRequestContext(scopes: ["https://ossrdbms-aad.database.windows.net/.default"]) { };
        dataSourceBuilder.UsePeriodicPasswordProvider(async (_, cancellationToken) =>
            (await psqlServerTokenProvider.GetTokenAsync(tokenRequestContext, cancellationToken)).Token, TimeSpan.FromMinutes(45), TimeSpan.FromSeconds(0)
        );

        return dataSourceBuilder.Build();
    }
}
