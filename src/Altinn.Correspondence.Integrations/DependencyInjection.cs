using Altinn.ApiClients.Maskinporten.Config;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.AccessManangement;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Altinn.Correspondence.Integrations.Altinn.Events;
using Altinn.Correspondence.Integrations.Altinn.Notifications;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
using Altinn.Correspondence.Integrations.Dialogporten;
using Altinn.Correspondence.Integrations.Settings;
using Altinn.Correspondence.Integrations.Slack;
using Altinn.Correspondence.Repositories;
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
        services.AddScoped<IAltinnAuthorizationService, AltinnAuthorizationService>();
        services.AddScoped<IResourceRightsService, ResourceRightsService>();
        services.AddScoped<IAltinnRegisterService, AltinnRegisterService>();
        services.AddScoped<IAltinnAccessManagementService, AltinnAccessManagementService>();
        if (string.IsNullOrWhiteSpace(maskinportenSettings.ClientId))
        {
            services.AddScoped<IEventBus, ConsoleLogEventBus>();
            services.AddScoped<IAltinnNotificationService, AltinnDevNotificationService>();
            services.AddScoped<IDialogportenService, DialogportenDevService>();
            services.AddScoped<IAltinnAuthorizationService, AltinnAuthorizationDevService>();
        } 
        else
        {
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            services.RegisterMaskinportenHttpClient<IAltinnAuthorizationService, AltinnAuthorizationService>(maskinportenSettings, altinnOptions);
            services.RegisterMaskinportenHttpClient<IResourceRightsService, ResourceRightsService>(maskinportenSettings, altinnOptions);
            services.RegisterMaskinportenHttpClient<IAltinnRegisterService, AltinnRegisterService>(maskinportenSettings, altinnOptions);
            services.RegisterMaskinportenHttpClient<IAltinnAccessManagementService, AltinnAccessManagementService>(maskinportenSettings, altinnOptions);
            services.RegisterMaskinportenHttpClient<IEventBus, AltinnEventBus>(maskinportenSettings, altinnOptions);
            services.RegisterMaskinportenHttpClient<IAltinnNotificationService, AltinnNotificationService>(maskinportenSettings, altinnOptions);
            services.RegisterMaskinportenHttpClient<IDialogportenService, DialogportenService>(maskinportenSettings, altinnOptions);
        }
        var generalSettings = new GeneralSettings();
        config.GetSection(nameof(GeneralSettings)).Bind(generalSettings);
        if (string.IsNullOrWhiteSpace(generalSettings.SlackUrl))
        {
            services.AddSingleton<ISlackClient>(new SlackDevClient(""));
        } 
        else
        {
            services.AddSingleton<ISlackClient>(new SlackClient(generalSettings.SlackUrl));
        }
    }

    public static void RegisterMaskinportenHttpClient<TClient, TImplementation>(this IServiceCollection services, MaskinportenSettings maskinportenSettings, AltinnOptions altinnOptions)
        where TClient : class
        where TImplementation : class, TClient
    {
        services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(TClient).FullName, maskinportenSettings);
        services.AddHttpClient<TClient, TImplementation>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, TClient>();
    }
}
