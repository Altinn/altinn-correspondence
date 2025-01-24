using System.Net;
using System.Net.Http.Json;
using Altinn.Correspondence.Repositories;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Altinn.Correspondence.Common.Helpers;
using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
public class ResourceRightsService : IResourceRightsService
{
    private readonly HttpClient _client;
    private readonly ILogger<ResourceRightsService> _logger;
    
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _cacheOptions;

    public ResourceRightsService(HttpClient httpClient, IOptions<AltinnOptions> options, ILogger<ResourceRightsService> logger, IDistributedCache cache)
    {
        httpClient.BaseAddress = new Uri(options.Value.PlatformGatewayUrl);
        _client = httpClient;
        _logger = logger;
        _cache = cache;
        _cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        };
    }

    public async Task<string?> GetServiceOwnerOfResource(string resourceId, CancellationToken cancellationToken)
    {
        string cacheKey = $"ServiceOwnerOfResource_{resourceId}";
        try 
        {
            string? cachedResource = await CacheHelpers.GetObjectFromCacheAsync<string>(cacheKey, _cache, cancellationToken);
            if (cachedResource != null)
            {
                return cachedResource;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving service owner from cache when looking up service owner of resource in Resource Rights Service.");
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
        var nameAttributes = new List<string> { "en", "nb-no", "nn-no" };
        string? name = null;
        foreach (var nameAttribute in nameAttributes)
        {
            if (altinnResourceResponse.HasCompetentAuthority.Name?.ContainsKey(nameAttribute) == true)
            {
                name = altinnResourceResponse.HasCompetentAuthority.Name[nameAttribute];
                break;
            }
        }
        if (name == null)
        {
            return name;
        }

        try
        {
            await CacheHelpers.StoreObjectInCacheAsync(cacheKey, name, _cache, _cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing service owner name to cache when looking up service owner of resource in Resource Rights Service.");
        }

        return name;
    }
}
