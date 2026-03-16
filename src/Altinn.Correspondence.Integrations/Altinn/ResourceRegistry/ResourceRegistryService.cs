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
        _logger.LogDebug("GetResourceType called for resourceId {resourceId}", resourceId.SanitizeForLogging());
        var altinnResourceResponse = await GetResource(resourceId, cancellationToken);
        if (altinnResourceResponse is null)
        {
            _logger.LogDebug("GetResourceType found no resource for resourceId {resourceId}", resourceId.SanitizeForLogging());
        }
        else
        {
            _logger.LogDebug("GetResourceType resolved resource type {resourceType} for resourceId {resourceId}", altinnResourceResponse.ResourceType, resourceId.SanitizeForLogging());
        }
        return altinnResourceResponse?.ResourceType;
    }

    public async Task<string?> GetServiceOwnerNameOfResource(string resourceId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("GetServiceOwnerNameOfResource called for resourceId {resourceId}", resourceId.SanitizeForLogging());
        var altinnResourceResponse = await GetResource(resourceId, cancellationToken);
        if (altinnResourceResponse is null)
        {
            _logger.LogDebug("GetServiceOwnerNameOfResource found no resource for resourceId {resourceId}", resourceId.SanitizeForLogging());
            return null;
        }
        var name = GetNameOfResourceResponse(altinnResourceResponse);
        _logger.LogDebug("GetServiceOwnerNameOfResource resolved name {name} for resourceId {resourceId}", name.SanitizeForLogging(), resourceId.SanitizeForLogging());
        return name;
    }

    public async Task<string?> GetResourceTitle(string resourceId, string? language = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetResourceTitle called for resourceId {resourceId} and language {language}", resourceId.SanitizeForLogging(), language);
        var altinnResourceResponse = await GetResource(resourceId, cancellationToken);
        if (altinnResourceResponse is null)
        {
            _logger.LogDebug("GetResourceTitle found no resource for resourceId {resourceId}", resourceId.SanitizeForLogging());
            return null;
        }

        var title = GetTitleOfResourceResponse(altinnResourceResponse, language);
        _logger.LogDebug("GetResourceTitle resolved title {title} for resourceId {resourceId} and language {language}", title.SanitizeForLogging(), resourceId.SanitizeForLogging(), language);
        return title;
    }

    public async Task<string?> GetServiceOwnerOrgCode(string resourceId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("GetServiceOwnerOrgCode called for resourceId {resourceId}", resourceId.SanitizeForLogging());
        var altinnResourceResponse = await GetResource(resourceId, cancellationToken);
        if (altinnResourceResponse is null)
        {
            _logger.LogDebug("GetServiceOwnerOrgCode found no resource for resourceId {resourceId}", resourceId.SanitizeForLogging());
            return null;
        }
        var orgCode = altinnResourceResponse.HasCompetentAuthority?.Orgcode ?? string.Empty;
        _logger.LogDebug("GetServiceOwnerOrgCode resolved org code {orgCode} for resourceId {resourceId}", orgCode.SanitizeForLogging(), resourceId.SanitizeForLogging());
        return orgCode;
    }

    private async Task<GetResourceResponse?> GetResource(string resourceId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("GetResource called for resourceId {resourceId}", resourceId.SanitizeForLogging());
        string cacheKey = CacheKey(resourceId);
        try
        {
            var cachedResource = await CacheHelpers.GetObjectFromCacheAsync<GetResourceResponse>(cacheKey, _cache, cancellationToken);
            if (cachedResource != null)
            {
                _logger.LogDebug("GetResource returned cached resource for resourceId {resourceId}", resourceId.SanitizeForLogging());
                return cachedResource;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving resource from cache.");
        }
        _logger.LogDebug("GetResource performing HTTP GET for resourceId {resourceId}", resourceId.SanitizeForLogging());
        var response = await _client.GetAsync($"resourceregistry/api/v1/resource/{resourceId.WithoutPrefix()}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
        {
            _logger.LogDebug("GetResource received {StatusCode} from Resource Registry for resourceId {resourceId}", response.StatusCode, resourceId.SanitizeForLogging());
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
        var nameAttributes = new List<string> { "nb", "nn", "en" };
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

    private static string GetTitleOfResourceResponse(GetResourceResponse resourceResponse, string? language)
    {
        if (resourceResponse.Title is null || resourceResponse.Title.Count == 0)
        {
            return string.Empty;
        }
        if (language == null)
        {
            return resourceResponse.Title.Values.FirstOrDefault() ?? string.Empty;
        }
        return resourceResponse.Title.TryGetValue(language, out var preferredTitle) ? preferredTitle : string.Empty;
    }

    public async Task<string?> GetServiceOwnerOrganizationNumber(string resourceId, CancellationToken cancellationToken = default)
    {
        var altinnResourceResponse = await GetResource(resourceId, cancellationToken);
        if (altinnResourceResponse is null)
        {
            return null;
        }
        return altinnResourceResponse.HasCompetentAuthority?.Organization;
    }


}
