using System.Net;
using System.Net.Http.Json;
using Altinn.Broker.Correspondence.Repositories;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
public class ResourceRightsService : IResourceRightsService
{
    private readonly HttpClient _client;
    private readonly ILogger<ResourceRightsService> _logger;

    public ResourceRightsService(HttpClient httpClient, IOptions<AltinnOptions> options, ILogger<ResourceRightsService> logger)
    {
        httpClient.BaseAddress = new Uri(options.Value.PlatformGatewayUrl);
        _client = httpClient;
        _logger = logger;
    }

    public async Task<bool> Exists(string resourceId, CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync($"resourceregistry/api/v1/resource/{resourceId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
        {
            return false;
        }
        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogError("Failed to get resource from Altinn Resource Registry. Status code: {StatusCode}\nBody: {Response}", response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            throw new BadHttpRequestException("Failed to get resource from Altinn Resource Registry");
        }
        var altinnResourceResponse = await response.Content.ReadFromJsonAsync<GetResourceResponse>(cancellationToken: cancellationToken);
        if (altinnResourceResponse is null)
        {
            _logger.LogError("Failed to deserialize response from Altinn Resource Registry");
            throw new BadHttpRequestException("Failed to process response from Altinn Resource Registry");
        }
        return true;
    }
}
