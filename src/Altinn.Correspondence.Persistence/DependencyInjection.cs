using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Repositories;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Altinn.Correspondence.Persistence;
public static class DependencyInjection
{
    public static void AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(BuildAzureNpgsqlDataSource(config));
        services.AddDbContext<ApplicationDbContext>(entityFrameworkConfig =>
        {
            entityFrameworkConfig.UseNpgsql();
        });
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IAttachmentStatusRepository, AttachmentStatusRepository>();
        services.AddScoped<ICorrespondenceRepository, CorrespondenceRepository>();
        services.AddScoped<ICorrespondenceStatusRepository, CorrespondenceStatusRepository>();
        services.AddScoped<ICorrespondenceNotificationRepository, CorrespondenceNotificationRepository>();
        services.AddScoped<IStorageRepository, StorageRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<ILegacyPartyRepository, LegacyPartyRepository>();
    }

    private static NpgsqlDataSource BuildAzureNpgsqlDataSource(IConfiguration config)
    {
        var databaseOptions = new DatabaseOptions() { ConnectionString = "" };
        config.GetSection(nameof(DatabaseOptions)).Bind(databaseOptions);
        var dataSourceBuilder = new NpgsqlDataSourceBuilder();
        dataSourceBuilder.ConnectionStringBuilder.ConnectionString = databaseOptions.ConnectionString;
        if (!string.IsNullOrWhiteSpace(dataSourceBuilder.ConnectionStringBuilder.Password))
        {
            return dataSourceBuilder.Build();
        }

        var psqlServerTokenProvider = new DefaultAzureCredential();
        var tokenRequestContext = new TokenRequestContext(scopes: ["https://ossrdbms-aad.database.windows.net/.default"]) { };
        dataSourceBuilder.UsePeriodicPasswordProvider(async (_, cancellationToken) =>
            psqlServerTokenProvider.GetTokenAsync(tokenRequestContext).Result.Token, TimeSpan.FromMinutes(45), TimeSpan.FromSeconds(0)
        );

        return dataSourceBuilder.Build();
    }
}
