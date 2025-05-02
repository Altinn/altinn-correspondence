using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Entities;

public class StorageProviderEntity
{
    public long Id { get; set; }
    public DateTimeOffset Created { get; set; }

    public StorageProviderType Type { get; set; }

    public required string ResourceName { get; set; }
    public required string ServiceOwnerId { get; set; }
    public required bool Active { get; set; }
}
