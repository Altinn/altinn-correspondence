using Altinn.Correspondence.Application.InitializeAttachmentCommand;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Application;
public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<InitializeAttachmentCommandHandler>();
    }
}
