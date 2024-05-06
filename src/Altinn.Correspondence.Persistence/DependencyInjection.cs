using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Attachments;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Persistence;
public static class DependencyInjection
{
    public static void AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
    }
}
