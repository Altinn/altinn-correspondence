using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Brreg.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Integrations.Brreg
{
    /// <summary>
    /// Service for interacting with Brønnøysundregistrene API
    /// </summary>
    public class BrregService : IBrregService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BrregService> _logger;
        private readonly GeneralSettings _settings;

        public BrregService(HttpClient httpClient, IOptions<GeneralSettings> settings, ILogger<BrregService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            if (!string.IsNullOrEmpty(_settings.BrregBaseUrl))
            {
                _httpClient.BaseAddress = new Uri(_settings.BrregBaseUrl);
            }
        }

        public async Task<bool> CheckOrganizationRolesAsync(string organizationNumber, IEnumerable<string> roletypes, CancellationToken cancellationToken = default)
        {
            var rolesResponse = await GetOrganizationRolesAsync(organizationNumber, cancellationToken);
            
            var registeredRoleTypes = ExtractRegisteredRoleTypes(rolesResponse);

            foreach (var requestedRole in roletypes)
            {
                foreach (var roleType in registeredRoleTypes)
                {
                    if (requestedRole.Equals(roleType, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public async Task<bool> IsOrganizationBankruptOrDeletedAsync(string organizationNumber, CancellationToken cancellationToken = default)
        {
            var details = await GetOrganizationDetailsAsync(organizationNumber, cancellationToken);
            
            bool isBankruptOrDeleted = details.IsBankrupt || details.IsDeleted;
            if (isBankruptOrDeleted)
            {
                _logger.LogInformation("Organization {OrganizationNumber} is {Status}", 
                    organizationNumber, 
                    details.IsBankrupt ? "bankrupt" : "deleted");
            }

            return isBankruptOrDeleted;
        }

        /// <summary>
        /// Gets all roles for an organization
        /// </summary>
        /// <param name="organizationNumber">The organization number</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Organization roles response</returns>
        /// <exception cref="HttpRequestException">Thrown when the API call fails</exception>
        internal async Task<OrganizationRolesResponse> GetOrganizationRolesAsync(string organizationNumber, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting roles for organization {OrganizationNumber}", organizationNumber);
            var endpoint = $"enheter/{organizationNumber}/roller";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get roles for organization {OrganizationNumber}. Status code: {StatusCode}, Error: {Error}", 
                    organizationNumber, response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to get roles for organization {organizationNumber}. Status code: {response.StatusCode}, Error: {errorContent}");
            }

            var rolesResponse = await response.Content.ReadFromJsonAsync<OrganizationRolesResponse>(cancellationToken: cancellationToken);
            if (rolesResponse == null)
            {
                _logger.LogError("Unexpected response format from Brreg API when getting roles for organization {OrganizationNumber}", organizationNumber);
                throw new HttpRequestException($"Unexpected response format from Brreg API when getting roles for organization {organizationNumber}");
            }
            
            return rolesResponse;
        }

        /// <summary>
        /// Gets details for an organization, including bankruptcy and deletion status
        /// </summary>
        /// <param name="organizationNumber">The organization number</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Organization details response</returns>
        /// <exception cref="HttpRequestException">Thrown when the API call fails</exception>
        internal async Task<OrganizationDetailsResponse> GetOrganizationDetailsAsync(string organizationNumber, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting details for organization {OrganizationNumber}", organizationNumber);
            var endpoint = $"enheter/{organizationNumber}";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get details for organization {OrganizationNumber}. Status code: {StatusCode}, Error: {Error}", 
                    organizationNumber, response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to get details for organization {organizationNumber}. Status code: {response.StatusCode}, Error: {errorContent}");
            }

            var detailsResponse = await response.Content.ReadFromJsonAsync<OrganizationDetailsResponse>(cancellationToken: cancellationToken);
            if (detailsResponse == null)
            {
                _logger.LogError("Unexpected response format from Brreg API when getting details for organization {OrganizationNumber}", organizationNumber);
                throw new HttpRequestException($"Unexpected response format from Brreg API when getting details for organization {organizationNumber}");
            }
            
            return detailsResponse;
        }

        /// <summary>
        /// Method to retrieve the registered role types from the organization roles response.
        /// </summary>
        /// <param name="response">The response from the roles request</param>
        /// <returns>A list of roletypes that have at least one user with the role registered</returns>
        private static List<string> ExtractRegisteredRoleTypes(OrganizationRolesResponse response)
        {
            var roles = new List<string>();

            if (response.RoleGroups == null)
            {
                return roles;
            }

            foreach (var roleGroup in response.RoleGroups)
            {
                if (roleGroup.Type?.Code == null || roleGroup.Roles == null)
                {
                    continue;
                }

                foreach (var role in roleGroup.Roles)
                {
                    if (role.Type?.Code != null && !role.HasResigned)
                    {
                        roles.Add(role.Type.Code);
                    }
                }
            }

            return roles;
        }
    }
}
