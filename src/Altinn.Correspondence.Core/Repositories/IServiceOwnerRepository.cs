using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IServiceOwnerRepository
    {
        Task<bool> InitializeNewServiceOwner(string orgNo, string name, CancellationToken cancellationToken);
        Task<ServiceOwnerEntity?> GetServiceOwnerByOrgNo(string orgNo, CancellationToken cancellationToken);
        Task<ServiceOwnerEntity?> GetServiceOwnerByOrgCode(string orgCode, CancellationToken cancellationToken);
        Task InitializeStorageProvider(string orgNo, string storageAccountName, StorageProviderType storageType);
    }
}
