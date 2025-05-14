namespace Altinn.Correspondence.Core.Services;

public interface IResourceRegistryService
{
    Task<string?> GetServiceOwnerNameOfResource(string resourceId, CancellationToken cancellationToken = default);
    Task<string?> GetResourceType(string resourceId, CancellationToken cancellationToken = default);
    Task<string?> GetServiceOwnerOrgCode(string resourceId, CancellationToken cancellationToken = default);
}
