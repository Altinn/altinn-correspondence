using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Repositories;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Persistence;
public static class DependencyInjection
{
    public static void AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IAttachmentStatusRepository, AttachmentStatusRepository>();
        services.AddScoped<ICorrespondenceRepository, CorrespondenceRepository>();
        services.AddScoped<IStorageRepository, StorageRepository>();
    }
}
