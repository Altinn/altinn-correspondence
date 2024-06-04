
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Altinn.Correspondence.Core.Options;


namespace Altinn.Correspondence.Integrations;
public static class DependencyInjection
{
    public static void AddIntegrations(this IServiceCollection services)
    {
        services.AddScoped<IEventBus, ConsoleLogEventBus>();
        //services.AddScoped<IEventBus, AltinnEventBus>();
    }
}
