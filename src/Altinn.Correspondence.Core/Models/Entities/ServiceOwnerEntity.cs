namespace Altinn.Correspondence.Core.Models.Entities;

public class ServiceOwnerEntity
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required List<StorageProviderEntity> StorageProviders { get; set; }
    public StorageProviderEntity? GetStorageProvider(bool withVirusScan)
    {
        return StorageProviders.FirstOrDefault(sp => sp.Type == (withVirusScan ? Enums.StorageProviderType.Altinn3Azure : Enums.StorageProviderType.Altinn3AzureWithoutVirusScan));
    }
}
