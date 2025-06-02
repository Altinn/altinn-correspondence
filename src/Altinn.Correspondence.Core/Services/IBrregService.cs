namespace Altinn.Correspondence.Core.Services
{
    using Altinn.Correspondence.Core.Models.Brreg;
    
    /// <summary>
    /// Service for interacting with Brønnøysundregistrene API
    /// </summary>
    public interface IBrregService
    {
        /// <summary>
        /// Gets detailed information about an organization
        /// </summary>
        /// <param name="organizationNumber">The organization number</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Organization details model</returns>
        /// <exception cref="HttpRequestException">Thrown when the API call fails</exception>
        Task<OrganizationDetails> GetOrganizationDetailsAsync(string organizationNumber, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets roles information for an organization
        /// </summary>
        /// <param name="organizationNumber">The organization number</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Organization roles model</returns>
        /// <exception cref="HttpRequestException">Thrown when the API call fails</exception>
        Task<OrganizationRoles> GetOrganizationRolesAsync(string organizationNumber, CancellationToken cancellationToken = default);
    }
}