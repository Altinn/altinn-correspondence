namespace Altinn.Correspondence.Repositories;

public interface IResourceRightsService
{
    Task<string?> GetServiceOwnerOfResource(string resourceId, CancellationToken cancellationToken = default);
}
