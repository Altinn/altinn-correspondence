using Altinn.ApiClients.Maskinporten.Config;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.AccessManagement;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Altinn.Correspondence.Integrations.Altinn.ContactReservationRegistry;
using Altinn.Correspondence.Integrations.Altinn.Events;
using Altinn.Correspondence.Integrations.Altinn.Notifications;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
using Altinn.Correspondence.Integrations.Altinn.Storage;
using Altinn.Correspondence.Integrations.Azure;
using Altinn.Correspondence.Integrations.Dialogporten;
using Altinn.Correspondence.Integrations.Slack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Slack.Webhooks;

namespace Altinn.Correspondence.Integrations;
public static class DependencyInjection
{
    public static void AddIntegrations(this IServiceCollection services, IConfiguration config)
    {
        var maskinportenSettings = new MaskinportenSettings();
        config.GetSection(nameof(MaskinportenSettings)).Bind(maskinportenSettings);
        var generalSettings = new GeneralSettings();
        config.GetSection(nameof(GeneralSettings)).Bind(generalSettings);
        services.AddScoped<IResourceManager, AzureResourceManagerService>();
        services.AddScoped<IStorageConnectionStringRepository, AzureResourceManagerService>();
        services.AddScoped<IResourceRegistryService, ResourceRegistryService>();
        services.AddSingleton<SasTokenCacheService, SasTokenCacheService>();
        if (string.IsNullOrWhiteSpace(maskinportenSettings.ClientId))
        {
            services.AddScoped<IEventBus, ConsoleLogEventBus>();
            services.AddScoped<IAltinnNotificationService, AltinnDevNotificationService>();
            services.AddScoped<IDialogportenService, DialogportenDevService>();
            services.AddScoped<IAltinnAuthorizationService, AltinnAuthorizationDevService>();
            services.AddScoped<IAltinnRegisterService, AltinnRegisterDevService>();
            services.AddScoped<IAltinnAccessManagementService, AltinnAccessManagementDevService>();
            services.AddScoped<IContactReservationRegistryService, ContactReservationRegistryDevService>();
            services.AddScoped<IAltinnStorageService, AltinnStorageDevService>();
        }
        else
        {
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            services.RegisterAltinnHttpClient<IAltinnAuthorizationService, AltinnAuthorizationService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IResourceRegistryService, ResourceRegistryService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IAltinnRegisterService, AltinnRegisterService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IAltinnAccessManagementService, AltinnAccessManagementService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IEventBus, AltinnEventBus>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IAltinnNotificationService, AltinnNotificationService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IDialogportenService, DialogportenService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IAltinnStorageService, AltinnStorageService>(maskinportenSettings, altinnOptions);
            services.RegisterMaskinportenHttpClient<IContactReservationRegistryService, ContactReservationRegistryService>(config, generalSettings.ContactReservationRegistryBaseUrl);
        }
        if (string.IsNullOrWhiteSpace(generalSettings.SlackUrl))
        {
            services.AddSingleton<ISlackClient>(new SlackDevClient(""));
        }
        else
        {
            services.AddSingleton<ISlackClient>(new SlackClient(generalSettings.SlackUrl));
        }
        
        services.AddSingleton<SlackSettings>();
    }

    public static void RegisterAltinnHttpClient<TClient, TImplementation>(this IServiceCollection services, MaskinportenSettings maskinportenSettings, AltinnOptions altinnOptions)
        where TClient : class
        where TImplementation : class, TClient
    {
        services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(TClient).FullName, maskinportenSettings);
        services.AddHttpClient<TClient, TImplementation>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, TClient>();
    }

    public static void RegisterMaskinportenHttpClient<TClient, TImplementation>(this IServiceCollection services, IConfiguration config, string baseAddress)
        where TClient : class
        where TImplementation : class, TClient
    {
        var maskinportenSettings = new MaskinportenSettings();
        config.GetSection(nameof(MaskinportenSettings)).Bind(maskinportenSettings);
        maskinportenSettings.ExhangeToAltinnToken = false;
        services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(TClient).FullName, maskinportenSettings);
        services.AddHttpClient<TClient, TImplementation>((client) => client.BaseAddress = new Uri(baseAddress))
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, TClient>();
    }
}
