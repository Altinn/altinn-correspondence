namespace Altinn.Correspondence.Core.Services
{
    /// <summary>
    /// Service for interacting with Brønnøysundregistrene API
    /// </summary>
    public interface IBrregService
    {
        /// <summary>
        /// Checks if an organization has the specified role(s)
        /// </summary>
        /// <param name="organizationNumber">The organization number</param>
        /// <param name="roles">The roles to check for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the organization has registered someone with one of the role(s), false otherwise</returns>
        /// <exception cref="HttpRequestException">Thrown when the API call fails</exception>
        Task<bool> HasAnyOfOrganizationRolesAsync(string organizationNumber, IEnumerable<string> roles, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if an organization is in bankruptcy or deleted
        /// </summary>
        /// <param name="organizationNumber">The organization number</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the organization is in bankruptcy or deleted, false otherwise</returns>
        /// <exception cref="HttpRequestException">Thrown when the API call fails</exception>
        Task<bool> IsOrganizationBankruptOrDeletedAsync(string organizationNumber, CancellationToken cancellationToken = default);
    }
}