using System.Net.Http.Json;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Register;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Register.Contracts;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
            _logger.LogWarning(ex, "Error retrieving party from cache when looking up party in Altinn Register Service.");
        }

        var parties = await QueryParties(new List<string> { identificationId }, cancellationToken);
        if (parties is null || parties.Count == 0)
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
            _logger.LogWarning(ex, "Error storing party in cache when looking up party in Altinn Register Service.");
        }

        return party;
    }
    
    public async Task<List<RoleItem>> LookUpPartyRoles(string partyUuid, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"register/api/v1/correspondence/parties/{partyUuid}/roles/correspondence-roles", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error when looking up party roles in Altinn Register. Statuscode was: {response.StatusCode}, error was: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var roles = await response.Content.ReadFromJsonAsync<Roles>(cancellationToken);
        if (roles is null)
        {
            throw new Exception("Unexpected json response when looking up party roles in Altinn Register");
        }

        return roles.Data ?? new List<RoleItem>();
    }

    public async Task<List<Party>?> LookUpPartiesByIds(List<string> identificationIds, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"PartiesByIds_{string.Join("_", identificationIds).GetHashCode()}";
        try
        {
            var cachedParties = await CacheHelpers.GetObjectFromCacheAsync<List<Party>>(cacheKey, _cache, cancellationToken);
            if (cachedParties != null)
            {
                return cachedParties;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving parties from cache when looking up parties in Altinn Register Service.");
        }

        var parties = await QueryParties(identificationIds, cancellationToken);
        if (parties is null)
        {
            return null;
        }

        try
        {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, parties, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing parties in cache when looking up parties in Altinn Register Service.");
        }

        return parties;
    }

    public async Task<List<MainUnitItem>> LookUpMainUnits(string urn, CancellationToken cancellationToken = default)
    {
        var request = new MainUnitsRequest { Data = urn };
        var response = await _httpClient.PostAsJsonAsync("register/api/v1/correspondence/parties/main-units", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error when looking up main-units in Altinn Register. Statuscode was: {response.StatusCode}, error was: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var result = await response.Content.ReadFromJsonAsync<MainUnitsResponse>(cancellationToken);
        if (result is null)
        {
            throw new Exception("Unexpected json response when looking up main-units in Altinn Register");
        }

        return result.Data ?? new List<MainUnitItem>();
    }

    /// <summary>
    /// Queries parties using the v2 query endpoint. Identifiers are normalized to URNs.
    /// Supports organization numbers, SSNs, party IDs, party UUIDs, and URNs.
    /// Deserializes directly into <see cref="Party"/> from Altinn.Register.Contracts;
    /// the polymorphic discriminator on <c>partyType</c> picks <see cref="Person"/>,
    /// <see cref="Organization"/>, etc.
    /// </summary>
    private async Task<List<Party>?> QueryParties(List<string> identificationIds, CancellationToken cancellationToken = default)
    {
        var partyUrns = identificationIds.Select(id => id.WithUrnPrefix()).ToList();

        var request = new AltinnRegisterQueryData<string>
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
                _logger.LogWarning("IdentificationIds did not have any valid identifiers");
                return new List<Party>();
            }

            throw new Exception($"Error when querying parties in Altinn Register. Statuscode was: {response.StatusCode}, error was: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var parties = await response.Content.ReadFromJsonAsync<AltinnRegisterQueryData<Party>>(cancellationToken);
        if (parties is null)
        {
            throw new Exception("Unexpected json response when querying parties in Altinn Register");
        }

        return parties.Data;
    }
}
