using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Correspondence.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.Correspondence.Integrations.Altinn.AccessManagement;

public class AltinnAccessManagementService : IAltinnAccessManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnAccessManagementService> _logger;
    private readonly IHybridCacheWrapper _cache;
    private readonly HybridCacheEntryOptions _cacheOptions;
    private readonly int _MAX_DEPTH_FOR_SUBUNITS = 20;

    public AltinnAccessManagementService(
        HttpClient httpClient, 
        IOptions<AltinnOptions> altinnOptions, 
        ILogger<AltinnAccessManagementService> logger,
        IHybridCacheWrapper cache)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.AccessManagementSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _cacheOptions = new HybridCacheEntryOptions()
        {
            Expiration = TimeSpan.FromMinutes(15)
        };
    }

    public async Task<List<Party>> GetAuthorizedParties(Party partyToRequestFor, string? userId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"AuthorizedParties_{partyToRequestFor.PartyId}_{userId}";
        try {
            var cachedParties = await CacheHelpers.GetObjectFromCacheAsync<List<Party>>(cacheKey, _cache, cancellationToken);
            if (cachedParties != null)
            {
                _logger.LogInformation("Retrieved {count} authorized parties from cache.", cachedParties.Count);
                return cachedParties;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving authorized parties from cache in Access Management Service.");
        }

        AuthorizedPartiesRequest request = new(partyToRequestFor, userId);
        _logger.LogInformation("PartyId {partyId} has partyType {partyType} with userId {userId}", partyToRequestFor.PartyId, request.Type, userId);
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
        List<Party> parties = new();
        foreach (var p in responseContent)
        {
            if (!p.onlyHierarchyElementWithNoAccess)
            {
                parties.Add(new Party
                {
                    PartyId = p.partyId,
                    PartyUuid = p.partyUuid,
                    OrgNumber = p.organizationNumber,
                    SSN = p.personId,
                    Resources = p.authorizedResources,
                    PartyTypeName = GetType(p.type),
                });
            }
            if (p.subunits != null && p.subunits.Count > 0)
            {
                parties.AddRange(GetPartiesFromSubunits(p.subunits));
            }
        }
        _logger.LogInformation("Retrieved {Count} authorized parties from Access Management Service.", parties.Count);

        try {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, parties, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing response content to cache when looking up authorized parties in Access Management Service.");
        }

        return parties;
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
    private List<Party> GetPartiesFromSubunits(List<AuthroizedPartiesResponse> subunits, int depth = 0)
    {
        List<Party> parties = new();
        if (depth > _MAX_DEPTH_FOR_SUBUNITS)
        {
            _logger.LogWarning("Max depth for subunits reached. Ignoring further subunits.");
            return parties;
        }
        foreach (var subunit in subunits)
        {
            parties.Add(new Party
            {

                PartyId = subunit.partyId,
                PartyUuid = subunit.partyUuid,
                OrgNumber = subunit.organizationNumber,
                SSN = subunit.personId,
                Resources = subunit.authorizedResources,
                PartyTypeName = GetType(subunit.type),
            });
            if (subunit.subunits != null && subunit.subunits.Count > 0)
            {
                parties.AddRange(GetPartiesFromSubunits(subunit.subunits, depth + 1));
            }
        }
        return parties;
    }

    internal sealed class AuthorizedPartiesRequest
    {
        public string Type { get; init; }
        public string Value { get; init; }

        public AuthorizedPartiesRequest(Party party, string? userId)
        {
            if (userId is not null)
            {
                Type = UrnConstants.UserId;
                Value = userId;
            }
            else if (party.PartyTypeName == PartyType.Person)
            {
                Type = UrnConstants.PersonIdAttribute;
                Value = party.SSN;
            }
            else
            {
                Type = UrnConstants.OrganizationNumberAttribute;
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
        public List<AuthroizedPartiesResponse> subunits { get; set; }
    }
}