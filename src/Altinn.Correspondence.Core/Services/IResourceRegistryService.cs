namespace Altinn.Correspondence.Core.Services;

public interface IResourceRegistryService
{
    Task<string?> GetServiceOwnerOfResource(string resourceId, CancellationToken cancellationToken = default);
    Task<string?> GetResourceType(string resourceId, CancellationToken cancellationToken = default);
    Task<string> GetServiceOwnerOrganizationId(string resourceId, CancellationToken cancellationToken = default);
}
