using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IStorageConnectionStringRepository
    {
        Task<string> GetStorageConnectionString(StorageProviderEntity storageProviderEntity);
    }
}
