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
using Altinn.Correspondence.Integrations.Redlock;
using Altinn.Correspondence.Integrations.Dialogporten;
using Altinn.Correspondence.Integrations.Slack;
using Altinn.Correspondence.Common.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Slack.Webhooks;
using Altinn.Correspondence.Integrations.Brreg;

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
        services.AddScoped<IResourceRegistryService, ResourceRegistryService>();
        services.AddSingleton<SasTokenService, SasTokenService>();
        
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
            services.AddScoped<IBrregService, BrregDevService>();
        }
        else
        {
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            var authorizationOptions = new AltinnOptions()
            {
                PlatformGatewayUrl = "http://altinn-authorization.default.svc.cluster.local"
            };
            services.RegisterAltinnHttpClient<IAltinnAuthorizationService, AltinnAuthorizationService>(maskinportenSettings, authorizationOptions);
            services.RegisterAltinnHttpClient<IResourceRegistryService, ResourceRegistryService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IAltinnRegisterService, AltinnRegisterService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IAltinnAccessManagementService, AltinnAccessManagementService>(maskinportenSettings, authorizationOptions);
            services.RegisterAltinnHttpClient<IEventBus, AltinnEventBus>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IAltinnNotificationService, AltinnNotificationService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IDialogportenService, DialogportenService>(maskinportenSettings, altinnOptions);
            services.RegisterAltinnHttpClient<IAltinnStorageService, AltinnStorageService>(maskinportenSettings, altinnOptions);
            services.RegisterMaskinportenHttpClient<IContactReservationRegistryService, ContactReservationRegistryService>(config, generalSettings.ContactReservationRegistryBaseUrl);
            services.AddHttpClient<IBrregService, BrregService>()
                .AddStandardRetryPolicy();
        }

        if (string.IsNullOrWhiteSpace(generalSettings.SlackUrl))
        {
            services.AddSingleton<ISlackClient>(new SlackDevClient(""));
        }
        else
        {
            services.AddHttpClient(nameof(SlackClient))
                .AddStandardRetryPolicy();
            services.AddSingleton<ISlackClient>(serviceProvider =>
            {
                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(SlackClient));
                return new SlackClient(generalSettings.SlackUrl, httpClient: httpClient);
            });
        }

        services.AddSingleton<SlackSettings>();
        services.AddSingleton<IDistributedLockHelper, DistributedLockHelper>();
    }

    public static void RegisterAltinnHttpClient<TClient, TImplementation>(this IServiceCollection services, MaskinportenSettings maskinportenSettings, AltinnOptions altinnOptions)
        where TClient : class
        where TImplementation : class, TClient
    {
        services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(TClient).FullName, maskinportenSettings);
        services.AddHttpClient<TClient, TImplementation>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, TClient>()
            .AddStandardRetryPolicy();
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
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, TClient>()
            .AddStandardRetryPolicy();
    }
}
