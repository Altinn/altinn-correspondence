using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Register.Contracts;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Integrations.Altinn.AccessManagement;

public class AltinnAccessManagementService : IAltinnAccessManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnAccessManagementService> _logger;
    private readonly IHybridCacheWrapper _cache;
    private readonly HybridCacheEntryOptions _cacheOptions;
    private const int MaxDepthForSubUnits = 20;

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

    public async Task<HashSet<int>> GetAuthorizedPartyIds(Party partyToRequestFor, string? userId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"AuthorizedParties_{partyToRequestFor.GetPartyId()}_{userId}";
        try
        {
            var cachedIds = await CacheHelpers.GetObjectFromCacheAsync<HashSet<int>>(cacheKey, _cache, cancellationToken);
            if (cachedIds != null)
            {
                return cachedIds;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving authorized parties from cache in Access Management Service.");
        }

        AuthorizedPartiesRequest request = new(partyToRequestFor, userId);
        _logger.LogDebug("PartyId {partyId} has type {partyType} with userId {userId}", partyToRequestFor.GetPartyId(), request.Type, userId);
        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };
        var response = await _httpClient.PostAsJsonAsync(
            "/accessmanagement/api/v1/resourceowner/authorizedparties?includeAltinn2=true&includeRoles=false&includeAccessPackages=false&includeInstances=false&includeResources=false",
            request,
            serializerOptions,
            cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(await response.Content.ReadAsStringAsync(cancellationToken));
            _logger.LogError("Error code in call against Authorization GetAuthorizedParties");
            throw new Exception("Error code in call against Authorization GetAuthorizedParties: " + response.StatusCode);
        }
        var responseContent = await response.Content.ReadFromJsonAsync<List<AuthorizedPartiesResponse>>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Authorization GetAuthorizedParties.");
            throw new Exception("Unexpected null or invalid json response from Authorization GetAuthorizedParties.");
        }

        var partyIds = new HashSet<int>();
        foreach (var p in responseContent)
        {
            CollectPartyIds(p, partyIds, depth: 0);
        }
        _logger.LogDebug("Retrieved {Count} authorized parties from Access Management Service.", partyIds.Count);

        try
        {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, partyIds, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing response content to cache when looking up authorized parties in Access Management Service.");
        }

        return partyIds;
    }

    private void CollectPartyIds(AuthorizedPartiesResponse src, HashSet<int> partyIds, int depth)
    {
        if (depth > MaxDepthForSubUnits)
        {
            _logger.LogWarning("Max depth for subunits reached. Ignoring further subunits.");
            return;
        }
        if (!src.onlyHierarchyElementWithNoAccess && src.partyId != 0)
        {
            partyIds.Add(src.partyId);
        }
        if (src.subunits is { Count: > 0 })
        {
            foreach (var sub in src.subunits)
            {
                CollectPartyIds(sub, partyIds, depth + 1);
            }
        }
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
            else if (party is Person p)
            {
                Type = UrnConstants.PersonIdAttribute;
                Value = p.PersonIdentifier.ToString();
            }
            else if (party is Organization o)
            {
                Type = UrnConstants.OrganizationNumberAttribute;
                Value = o.OrganizationIdentifier.ToString();
            }
            else
            {
                throw new ArgumentException($"Unsupported party type for authorized parties request: {party.GetType().Name}", nameof(party));
            }
        }
    }

    internal sealed class AuthorizedPartiesResponse
    {
        public int partyId { get; set; }
        public bool onlyHierarchyElementWithNoAccess { get; set; }
        public List<AuthorizedPartiesResponse>? subunits { get; set; }
    }
}
