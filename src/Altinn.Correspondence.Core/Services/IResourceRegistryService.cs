namespace Altinn.Correspondence.Core.Services;

public interface IResourceRegistryService
{
    /// <summary>
    /// Get the name of the service owner of the resource, for example Digitaliseringsdirektoratet, NAV, Skatteetaten, etc.
    /// </summary>
    /// <param name="resourceId">The id of the resource to get the service owner name for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name of the service owner of the resource.</returns>
    Task<string?> GetServiceOwnerNameOfResource(string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the resource title (name) from Resource Registry, localized if possible.
    /// </summary>
    /// <param name="resourceId">The id of the resource to get the title for.</param>
    /// <param name="language">
    /// Preferred language code (for example: "nb", "nn", "en").
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resource title, or null if the resource was not found.</returns>
    Task<string?> GetResourceTitle(string resourceId, string? language = null, CancellationToken cancellationToken = default);
    
    Task<string?> GetResourceType(string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the organization code of the service owner of the resource. Organization code is the internal id for a service owner.
    /// </summary>
    /// <param name="resourceId">The id of the resource to get the service owner org code for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The organization code of the service owner of the resource.</returns>
    Task<string?> GetServiceOwnerOrgCode(string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the organization number (9 digits) of the service owner of the resource.
    /// </summary>
    /// <param name="resourceId">The id of the resource to get the service owner org number for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The organization number of the service owner of the resource.</returns>
    Task<string?> GetServiceOwnerOrganizationNumber(string resourceId, CancellationToken cancellationToken = default);

}
