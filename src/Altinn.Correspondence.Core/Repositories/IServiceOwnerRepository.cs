using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IServiceOwnerRepository
    {
        Task InitializeServiceOwner(string orgNo, string name, CancellationToken cancellationToken);
        Task<ServiceOwnerEntity?> GetServiceOwner(string orgNo, CancellationToken cancellationToken);
        Task InitializeStorageProvider(string orgNo, string storageAccountName, StorageProviderType storageType);
    }
}
