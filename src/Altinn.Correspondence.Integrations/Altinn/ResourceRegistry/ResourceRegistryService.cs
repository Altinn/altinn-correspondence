using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
public class ResourceRegistryService : IResourceRegistryService
{
    private readonly HttpClient _client;
    private readonly ILogger<ResourceRegistryService> _logger;

    private readonly IHybridCacheWrapper _cache;
    private readonly HybridCacheEntryOptions _cacheOptions;
    private string CacheKey(string resourceId) => $"ResourceInfo_{resourceId}";

    public ResourceRegistryService(HttpClient httpClient, IOptions<AltinnOptions> options, ILogger<ResourceRegistryService> logger, IHybridCacheWrapper cache)
    {
        httpClient.BaseAddress = new Uri(options.Value.PlatformGatewayUrl);
        _client = httpClient;
        _logger = logger;
        _cache = cache;
        _cacheOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(10)
        };
    }

    public async Task<string?> GetResourceType(string resourceId, CancellationToken cancellationToken)
    {
        var altinnResourceResponse = await GetResource(resourceId, cancellationToken);
        return altinnResourceResponse?.ResourceType;
    }

    public async Task<string?> GetServiceOwnerOfResource(string resourceId, CancellationToken cancellationToken)
    {
        var altinnResourceResponse = await GetResource(resourceId, cancellationToken);
        if (altinnResourceResponse is null)
        {
            return null;
        }
        return GetNameOfResourceResponse(altinnResourceResponse);
    }

    public async Task<string> GetServiceOwnerOrganizationId(string resourceId, CancellationToken cancellationToken)
    {
        var altinnResourceResponse = await GetResource(resourceId, cancellationToken);
        if (altinnResourceResponse is null)
        {
            return null;
        }
        return altinnResourceResponse.HasCompetentAuthority.Organization ?? string.Empty;
    }

    private async Task<GetResourceResponse?> GetResource(string resourceId, CancellationToken cancellationToken)
    {
        string cacheKey = CacheKey(resourceId);
        try
        {
            var cachedResource = await CacheHelpers.GetObjectFromCacheAsync<GetResourceResponse>(cacheKey, _cache, cancellationToken);
            if (cachedResource != null)
            {
                return cachedResource;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving resource from cache.");
        }
        var response = await _client.GetAsync($"resourceregistry/api/v1/resource/{resourceId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }
        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogError("Failed to get resource from Altinn Resource Registry. Status code: {StatusCode}", response.StatusCode);
            _logger.LogError("Body: {Response}", await response.Content.ReadAsStringAsync(cancellationToken));
            throw new BadHttpRequestException("Failed to get resource from Altinn Resource Registry");
        }
        var altinnResourceResponse = await response.Content.ReadFromJsonAsync<GetResourceResponse>(cancellationToken: cancellationToken);
        if (altinnResourceResponse is null)
        {
            _logger.LogError("Failed to deserialize response from Altinn Resource Registry");
            throw new BadHttpRequestException("Failed to process response from Altinn Resource Registry");
        }
        try
        {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, altinnResourceResponse, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing resource to cache when looking up resource in Resource Registry Service.");
        }
        return altinnResourceResponse;
    }

    private string GetNameOfResourceResponse(GetResourceResponse resourceResponse)
    {
        var nameAttributes = new List<string> { "en", "nb-no", "nn-no" };
        string? name = null;
        foreach (var nameAttribute in nameAttributes)
        {
            if (resourceResponse.HasCompetentAuthority?.Name?.ContainsKey(nameAttribute) == true)
            {
                name = resourceResponse.HasCompetentAuthority.Name[nameAttribute];
                break;
            }
        }
        return name ?? string.Empty;
    }
}
