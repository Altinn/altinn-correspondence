using Altinn.ApiClients.Maskinporten.Config;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Correspondence.Repositories;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Altinn.Correspondence.Integrations.Altinn.Events;
using Altinn.Correspondence.Integrations.Altinn.Notifications;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Altinn.Correspondence.Integrations.Dialogporten;
using Slack.Webhooks;
using Altinn.Correspondence.Integrations.Settings;
using Altinn.Correspondence.Integrations.Slack;
using Altinn.Correspondence.Integrations.Altinn.AccessManangement;

namespace Altinn.Correspondence.Integrations;
public static class DependencyInjection
{
    public static void AddIntegrations(this IServiceCollection services, IConfiguration config, IHostEnvironment hostEnvironment)
    {
        services.AddScoped<IAltinnAuthorizationService, AltinnAuthorizationService>();
        services.AddScoped<IResourceRightsService, ResourceRightsService>();
        services.AddScoped<IAltinnRegisterService, AltinnRegisterService>();
        services.AddScoped<IAltinnAccessManagementService, AltinnAccessManagementService>();
        var generalSettings = new GeneralSettings();
        config.GetSection(nameof(GeneralSettings)).Bind(generalSettings);
        if (hostEnvironment.IsDevelopment())
        {
            services.AddScoped<IEventBus, ConsoleLogEventBus>();
            services.AddScoped<IAltinnNotificationService, AltinnDevNotificationService>();
            services.AddScoped<IDialogportenService, DialogportenDevService>();
            services.AddSingleton<ISlackClient>(new SlackDevClient(generalSettings.SlackUrl));
        }
        else
        {
            var maskinportenSettings = new MaskinportenSettings();
            config.GetSection(nameof(MaskinportenSettings)).Bind(maskinportenSettings);
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IEventBus).FullName, maskinportenSettings);
            services.AddHttpClient<IEventBus, AltinnEventBus>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IEventBus>();

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IResourceRightsService).FullName, maskinportenSettings);
            services.AddHttpClient<IResourceRightsService, ResourceRightsService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IResourceRightsService>();

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IAltinnRegisterService).FullName, maskinportenSettings);
            services.AddHttpClient<IAltinnRegisterService, AltinnRegisterService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IAltinnRegisterService>();

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IAltinnAuthorizationService).FullName, maskinportenSettings);
            services.AddHttpClient<IAltinnAuthorizationService, AltinnAuthorizationService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                    .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IAltinnAuthorizationService>();

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IDialogportenService).FullName, maskinportenSettings);
            services.AddHttpClient<IDialogportenService, DialogportenService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IDialogportenService>();

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IAltinnNotificationService).FullName, maskinportenSettings);
            services.AddHttpClient<IAltinnNotificationService, AltinnNotificationService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                    .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IAltinnNotificationService>();
            services.AddSingleton<ISlackClient>(new SlackClient(generalSettings.SlackUrl));

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IAltinnAccessManagementService).FullName, maskinportenSettings);
            services.AddHttpClient<IAltinnAccessManagementService, AltinnAccessManagementService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                    .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IAltinnAccessManagementService>();
        }
    }
}
