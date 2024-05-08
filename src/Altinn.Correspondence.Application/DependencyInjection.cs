using Altinn.Correspondence.Application.InitializeAttachmentCommand;
using Altinn.Correspondence.Application.InitializeCorrespondenceCommand;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Application;
public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<InitializeAttachmentCommandHandler>();
        services.AddScoped<InitializeCorrespondenceCommandHandler>();
    }
}
