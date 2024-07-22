namespace Altinn.Broker.Correspondence.Repositories;

public interface IResourceRegistryRepository
{
    Task<bool> Exists(string resourceId, CancellationToken cancellationToken = default);
}
