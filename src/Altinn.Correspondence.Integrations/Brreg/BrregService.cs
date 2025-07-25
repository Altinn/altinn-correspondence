using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using Altinn.Correspondence.Core.Models.Brreg;
using Altinn.Correspondence.Core.Exceptions;

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

        public async Task<OrganizationRoles> GetOrganizationRoles(string organizationNumber, CancellationToken cancellationToken = default)
        {
            var endpoint = $"enheter/{organizationNumber}/roller";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Organization {OrganizationNumber} not found in Brreg", organizationNumber);
                    throw new BrregNotFoundException(organizationNumber);
                }
                
                _logger.LogError("Failed to get roles for organization {OrganizationNumber}. Status code: {StatusCode}, Error: {Error}", 
                    organizationNumber, response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to get roles for organization {organizationNumber}. Status code: {response.StatusCode}, Error: {errorContent}");
            }

            var rolesResponse = await response.Content.ReadFromJsonAsync<OrganizationRoles>(cancellationToken: cancellationToken);
            if (rolesResponse == null)
            {
                _logger.LogError("Unexpected response format from Brreg API when getting roles for organization {OrganizationNumber}", organizationNumber);
                throw new HttpRequestException($"Unexpected response format from Brreg API when getting roles for organization {organizationNumber}");
            }
            
            return rolesResponse;
        }

        public async Task<OrganizationDetails> GetOrganizationDetails(string organizationNumber, CancellationToken cancellationToken = default)
        {
            var endpoint = $"enheter/{organizationNumber}";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Organization {OrganizationNumber} not found in Brreg", organizationNumber);
                    throw new BrregNotFoundException(organizationNumber);
                }
                
                _logger.LogError("Failed to get details for organization {OrganizationNumber}. Status code: {StatusCode}, Error: {Error}", 
                    organizationNumber, response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to get details for organization {organizationNumber}. Status code: {response.StatusCode}, Error: {errorContent}");
            }

            var detailsResponse = await response.Content.ReadFromJsonAsync<OrganizationDetails>(cancellationToken: cancellationToken);
            if (detailsResponse == null)
            {
                _logger.LogError("Unexpected response format from Brreg API when getting details for organization {OrganizationNumber}", organizationNumber);
                throw new HttpRequestException($"Unexpected response format from Brreg API when getting details for organization {organizationNumber}");
            }
            
            return detailsResponse;
        }

        public async Task<SubOrganizationDetails> GetSubOrganizationDetails(string organizationNumber, CancellationToken cancellationToken = default)
        {
            var endpoint = $"underenheter/{organizationNumber}";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Sub organization with organization number {OrganizationNumber} not found in Brreg", organizationNumber);
                    throw new BrregNotFoundException(organizationNumber);
                }
                
                _logger.LogError("Failed to get sub organization details for organization {OrganizationNumber}. Status code: {StatusCode}, Error: {Error}", 
                    organizationNumber, response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to get sub organization details for organization {organizationNumber}. Status code: {response.StatusCode}, Error: {errorContent}");
            }

            var detailsResponse = await response.Content.ReadFromJsonAsync<SubOrganizationDetails>(cancellationToken: cancellationToken);
            if (detailsResponse == null)
            {
                _logger.LogError("Unexpected response format from Brreg API when getting sub organization details for organization {OrganizationNumber}", organizationNumber);
                throw new HttpRequestException($"Unexpected response format from Brreg API when getting sub organization details for organization {organizationNumber}");
            }

            return detailsResponse;
        }
    }
}
