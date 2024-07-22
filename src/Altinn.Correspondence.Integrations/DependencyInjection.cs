using Altinn.Broker.Correspondence.Repositories;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Altinn.Correspondence.Integrations.Altinn.Events;
using Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Integrations;
public static class DependencyInjection
{
    public static void AddIntegrations(this IServiceCollection services)
    {
        services.AddScoped<IEventBus, ConsoleLogEventBus>();
        //services.AddScoped<IEventBus, AltinnEventBus>();
        services.AddScoped<IAltinnAuthorizationService, AltinnAuthorizationService>();
        services.AddScoped<IResourceRegistryRepository, ResourceRegistryRepository>();
    }
}
