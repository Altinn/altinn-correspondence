﻿using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
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
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _cacheOptions;

    public AltinnAccessManagementService(
        HttpClient httpClient, 
        IOptions<AltinnOptions> altinnOptions, 
        ILogger<AltinnAccessManagementService> logger,
        IDistributedCache cache)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.AccessManagementSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        };
    }

    public async Task<List<Party>> GetAuthorizedParties(Party partyToRequestFor, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"AuthorizedParties_{partyToRequestFor.PartyId}";
        try {
            string? cachedDataString = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedDataString))
            {
                return JsonSerializer.Deserialize<List<Party>>(cachedDataString) ?? new List<Party>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving authorized parties from cache. Proceeding with API call.");
        }

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

        var parties = responseContent.Select(p => new Party
        {
            PartyId = p.partyId,
            PartyUuid = p.partyUuid,
            OrgNumber = p.organizationNumber,
            SSN = p.personId,
            Resources = p.authorizedResources,
            PartyTypeName = GetType(p.type)
        }).ToList();

        try {
            string serializedDataString = JsonSerializer.Serialize(parties);
            await _cache.SetStringAsync(cacheKey, serializedDataString, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error saving response content from Authorization GetAuthorizedParties to cache.");
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

    internal sealed class AuthorizedPartiesRequest
    {
        public string Type { get; init; }
        public string Value { get; init; }

        public AuthorizedPartiesRequest(Party party)
        {
            if (party.PartyTypeName == PartyType.Person)
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