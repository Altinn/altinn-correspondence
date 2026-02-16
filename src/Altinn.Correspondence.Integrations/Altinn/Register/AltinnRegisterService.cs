
using System.Net.Http.Json;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Models.Register;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Altinn.Platform.Register.Models;

namespace Altinn.Correspondence.Integrations.Altinn.Register;
public class AltinnRegisterService : IAltinnRegisterService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnRegisterService> _logger;
    private readonly IHybridCacheWrapper _cache;
    private readonly HybridCacheEntryOptions _cacheOptions;

    public AltinnRegisterService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnRegisterService> logger, IHybridCacheWrapper cache)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _cacheOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromHours(24)
        };
    }

    public async Task<int?> LookUpPartyId(string identificationId, CancellationToken cancellationToken = default)
    {
        var party = await LookUpPartyById(identificationId, cancellationToken);
        return party?.PartyId;
    }

    public async Task<string?> LookUpName(string identificationId, CancellationToken cancellationToken = default)
    {
        var party = await LookUpPartyById(identificationId, cancellationToken);
        return party?.Name;
    }

    public async Task<Party?> LookUpPartyByPartyId(int partyId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"PartyByPartyId_{partyId}";
        try 
        {
            var cachedParty = await CacheHelpers.GetObjectFromCacheAsync<Party>(cacheKey, _cache, cancellationToken);
            if (cachedParty != null)
            {
                return cachedParty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving Party from cache when looking up Party in Altinn Register Service.");
        }

        if (partyId <= 0)
        {
            _logger.LogError("partyId is not a valid number.");
            return null;
        }

        var response = await _httpClient.GetAsync($"register/api/v1/parties/{partyId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error when looking up party in Altinn Register.Statuscode was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync());
            return null;
        }
        var party = await response.Content.ReadFromJsonAsync<Party>();
        if (party is null)
        {
            _logger.LogError("Unexpected json response when looking up Party in Altinn Register");
            return null;
        }

        try 
        {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, party, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing response content to cache when looking up Party in Altinn Register Service.");
        }

        return party;
    }

    public async Task<Party?> LookUpPartyByPartyUuid(Guid partyUuid, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"PartyByPartyUuid_{partyUuid}";
        try
        {
            var cachedParty = await CacheHelpers.GetObjectFromCacheAsync<Party>(cacheKey, _cache, cancellationToken);
            if (cachedParty != null)
            {
                return cachedParty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving Party by Uuid from cache when looking up Party in Altinn Register Service.");
        }

        var response = await _httpClient.GetAsync($"register/api/v1/parties/byuuid/{partyUuid}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error when looking up party by Uuid in Altinn Register.Statuscode was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync());
            return null;
        }
        var party = await response.Content.ReadFromJsonAsync<Party>();
        if (party is null)
        {
            _logger.LogError("Unexpected json response when looking up Party by Uuid in Altinn Register");
            return null;
        }

        try
        {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, party, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing response content to cache when looking up Party in Altinn Register Service.");
        }

        return party;
    }

    public async Task<Party?> LookUpPartyById(string identificationId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"PartyById_{identificationId}";
        try
        {
            var cachedParty = await CacheHelpers.GetObjectFromCacheAsync<Party>(cacheKey, _cache, cancellationToken);
            if (cachedParty != null)
            {
                return cachedParty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving organization from cache when looking up organization in Altinn Register Service.");
        }

        if (identificationId.IsPartyId())
        {
            if (int.TryParse(identificationId.WithoutPrefix(), out int partyId))
            {
                return await LookUpPartyByPartyId(partyId, cancellationToken);
            }
            else
            {
                _logger.LogError("identificationId is not a valid party id.");
                return null;
            }
        }

        identificationId = identificationId.WithoutPrefix();

        if (!identificationId.IsOrganizationNumber() && !identificationId.IsSocialSecurityNumber())
        {
            _logger.LogError("IdentificationId {identificationId} is not a valid organization number or social security number.", identificationId);
            return null;
        }

        var partyLookup = new PartyLookup()
        {
            OrgNo = identificationId.IsOrganizationNumber() ? identificationId : null,
            Ssn = identificationId.IsSocialSecurityNumber() ? identificationId : null
        };
        var response = await _httpClient.PostAsJsonAsync("register/api/v1/parties/lookup", partyLookup, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            throw new Exception($"Error when looking up organization in Altinn Register.Statuscode was: {response.StatusCode}, error was: {await response.Content.ReadAsStringAsync()}");
        }

        var party = await response.Content.ReadFromJsonAsync<Party>();
        if (party is null)
        {
            throw new Exception("Unexpected json response when looking up organization in Altinn Register");
        }

        try
        {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, party, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing response content to cache when looking up organization in Altinn Register Service.");
        }

        return party;
    }

    public async Task<PartyV2?> LookUpPartyV2ById(string identificationId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"PartyV2ById_{identificationId}";
        try
        {
            var cachedParty = await CacheHelpers.GetObjectFromCacheAsync<PartyV2>(cacheKey, _cache, cancellationToken);
            if (cachedParty != null)
            {
                return cachedParty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving organization from cache when looking up organization in Altinn Register Service.");
        }

        var parties = await QueryParties(new List<string> { identificationId }, cancellationToken);
        if (parties == null || parties.Count == 0)
        {
            return null;
        }

        var party = parties[0];

        try
        {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, party, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing response content to cache when looking up organization in Altinn Register Service.");
        }

        return party;
    }

    public async Task<List<RoleItem>> LookUpPartyRoles(string partyUuid, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"register/api/v1/correspondence/parties/{partyUuid}/roles/correspondence-roles", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error when looking up party roles in Altinn Register.Statuscode was: {response.StatusCode}, error was: {await response.Content.ReadAsStringAsync()}");
        }

        var roles = await response.Content.ReadFromJsonAsync<Roles>(cancellationToken: cancellationToken);
        if (roles is null)
        {
            throw new Exception("Unexpected json response when looking up party roles in Altinn Register");
        }

        return roles.Data ?? new List<RoleItem>();
    }

    public async Task<List<PartyV2>?> LookUpPartiesByIds(List<string> identificationIds, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"PartiesByIds_{string.Join("_", identificationIds).GetHashCode()}";
        try
        {
            var cachedParty = await CacheHelpers.GetObjectFromCacheAsync<List<PartyV2>>(cacheKey, _cache, cancellationToken);
            if (cachedParty != null)
            {
                return cachedParty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving party names from cache when looking up party names in Altinn Register Service.");
        }

        var parties = await QueryParties(identificationIds, cancellationToken);
        if (parties == null)
        {
            return null;
        }

        try
        {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, parties, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing response content to cache when looking up party names in Altinn Register Service.");
        }

        return parties;
    }

    public async Task<List<MainUnitItem>> LookUpMainUnits(string urn, CancellationToken cancellationToken = default)
    {
        var request = new MainUnitsRequest { Data = urn };
        var response = await _httpClient.PostAsJsonAsync("register/api/v1/correspondence/parties/main-units", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error when looking up main-units in Altinn Register.Statuscode was: {response.StatusCode}, error was: {await response.Content.ReadAsStringAsync()}");
        }
        var result = await response.Content.ReadFromJsonAsync<MainUnitsResponse>(cancellationToken: cancellationToken);
        if (result is null)
        {
            throw new Exception("Unexpected json response when looking up main-units in Altinn Register");
        }

        return result.Data ?? new List<MainUnitItem>();
    }

    /// <summary>
    /// Queries parties based on the provided identifiers using the V2 API.
    /// Supports organization numbers, SSNs, party IDs, party UUIDs, and email URNs.
    /// </summary>
    /// <param name="identificationIds">The party identifiers to look up.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of parties matching the provided identifiers.</returns>
    private async Task<List<PartyV2>?> QueryParties(List<string> identificationIds, CancellationToken cancellationToken = default)
    {
        var partyUrns = identificationIds.Select(id => id.WithUrnPrefix()).ToList();

        var request = new ListObject<string>
        {
            Data = partyUrns
        };

        var response = await _httpClient.PostAsJsonAsync(
            "register/api/v1/correspondence/parties/query?fields=identifiers&fields=display-name&fields=user",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound || 
                response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                _logger.LogWarning("IdentificationIds did not have any valid identifers");
                return new List<PartyV2>();
            }
            
            throw new Exception($"Error when querying parties in Altinn Register. Statuscode was: {response.StatusCode}, error was: {await response.Content.ReadAsStringAsync()}");
        }

        var partiesV2Response = await response.Content.ReadFromJsonAsync<ListObject<PartyV2>>(cancellationToken: cancellationToken);
        if (partiesV2Response is null)
        {
            throw new Exception("Unexpected json response when querying parties in Altinn Register");
        }
        
        return partiesV2Response.Data;
    }
}
