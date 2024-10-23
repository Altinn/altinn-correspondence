using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Altinn.AccessManagement;

public class AltinnAccessManagementService : IAltinnAccessManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnAccessManagementService> _logger;

    public AltinnAccessManagementService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnAccessManagementService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.AccessManagementSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<Party>> GetAuthorizedParties(Party partyToRequestFor, CancellationToken cancellationToken = default)
    {
        AuthorizedPartiesRequest request = new(partyToRequestFor);
        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };
        var response = await _httpClient.PostAsJsonAsync("/accessmanagement/api/v1/resourceowner/authorizedparties?includeAltinn2=true", request, serializerOptions, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(await response.Content.ReadAsStringAsync(cancellationToken));
            _logger.LogError("Error code in call against Authorization GetAuthorizedParties");
            throw new Exception("Error code in call against Authorization GetAuthorizedParties: " + response.StatusCode);
        }
        var responseContent = await response.Content.ReadFromJsonAsync<List<AuthroizedPartiesResponse>>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Authorization GetAuthorizedParties.");
            throw new Exception("Unexpected null or invalid json response from Authorization GetAuthorizedParties.");
        }

        return responseContent.Select(p => new Party
        {
            PartyId = p.partyId,
            PartyUuid = p.partyUuid,
            OrgNumber = p.organizationNumber,
            SSN = p.personId,
            Resources = p.authorizedResources,
            PartyTypeName = GetType(p.type)
        }).ToList();
    }
    public PartyType GetType(string type)
    {
        return type switch
        {
            "Person" => PartyType.Person,
            "Organization" => PartyType.Organization,
            "SelfIdentified" => PartyType.SelfIdentified,
            _ => throw new NotImplementedException()
        };
    }

    internal sealed class AuthorizedPartiesRequest
    {
        public string Type { get; init; }
        public string Value { get; init; }

        public AuthorizedPartiesRequest(Party party)
        {
            if (party.PartyTypeName == PartyType.Person)
            {
                Type = "urn:altinn:person:identifier-no";
                Value = party.SSN;
            }
            else
            {
                Type = "urn:altinn:organization:identifier-no";
                Value = party.OrgNumber;
            }
        }
    }
    internal sealed class AuthroizedPartiesResponse
    {

        public Guid partyUuid { get; set; }
        public string name { get; set; }
        public string organizationNumber { get; set; }
        public string personId { get; set; }
        public string type { get; set; }
        public int partyId { get; set; }
        public string unitType { get; set; }
        public bool isDeleted { get; set; }
        public bool onlyHierarchyElementWithNoAccess { get; set; }
        public List<string> authorizedResources { get; set; }
        public List<string> authorizedRoles { get; set; }
        public List<string> subunits { get; set; }
    }
}