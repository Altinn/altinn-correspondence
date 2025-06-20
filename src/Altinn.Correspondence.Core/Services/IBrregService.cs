using Altinn.Correspondence.Core.Models.Brreg;
using Altinn.Correspondence.Core.Exceptions;

namespace Altinn.Correspondence.Core.Services
{
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
        /// <exception cref="BrregNotFoundException">Thrown when the organization is not found</exception>
        /// <exception cref="HttpRequestException">Thrown when the API call fails for other reasons</exception>
        Task<OrganizationDetails> GetOrganizationDetailsAsync(string organizationNumber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information about a sub organization
        /// </summary>
        /// <param name="organizationNumber">The organization number for the sub organization</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Organization details model</returns>
        /// <exception cref="BrregNotFoundException">Thrown when the sub organization is not found</exception>
        /// <exception cref="HttpRequestException">Thrown when the API call fails for other reasons</exception>
        Task<SubOrganizationDetails> GetSubOrganizationDetailsAsync(string organizationNumber, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets roles information for an organization
        /// </summary>
        /// <param name="organizationNumber">The organization number</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Organization roles model</returns>
        /// <exception cref="BrregNotFoundException">Thrown when the organization is not found</exception>
        /// <exception cref="HttpRequestException">Thrown when the API call fails for other reasons</exception>
        Task<OrganizationRoles> GetOrganizationRolesAsync(string organizationNumber, CancellationToken cancellationToken = default);
    }
}