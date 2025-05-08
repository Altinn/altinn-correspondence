namespace Altinn.Correspondence.Core.Models.Entities;

public class ServiceOwnerEntity
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required List<StorageProviderEntity> StorageProviders { get; set; }
}
