using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Altinn.Events.Helpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Integrations.Altinn.Events;
public class AltinnEventBus : IEventBus
{
    private readonly AltinnOptions _altinnOptions;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnEventBus> _logger;

    public AltinnEventBus(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnEventBus> logger)
    {
        _httpClient = httpClient;
        _altinnOptions = altinnOptions.Value;
        _logger = logger;
    }

    public async Task Publish(AltinnEventType type, string resourceId, string itemId, string eventSource, string? organizationId = null, CancellationToken cancellationToken = default)
    {
        string? partyId = null;
        // TODO: Get party id

        var cloudEvent = CreateCloudEvent(type, resourceId, itemId, partyId, organizationId, eventSource);
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new LowerCaseNamingPolicy()
        };
        var response = await _httpClient.PostAsync("events/api/v1/events", JsonContent.Create(cloudEvent, options: serializerOptions, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/cloudevents+json")), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unexpected null or invalid json response when posting cloud event {type} of {resourceId} with {eventSource} id  {itemId}.", type, resourceId, eventSource, itemId);
            _logger.LogError("Statuscode was: {}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private CloudEvent CreateCloudEvent(AltinnEventType type, string resourceId, string itemId, string? partyId, string? alternativeSubject, string eventSource)
    {
        CloudEvent cloudEvent = new CloudEvent()
        {
            Id = Guid.NewGuid(),
            SpecVersion = "1.0",
            Time = DateTime.UtcNow,
            Resource = "urn:altinn:resource:" + resourceId,
            ResourceInstance = itemId,
            Type = "no.altinn.correspondence." + type.ToString().ToLowerInvariant(),
            Source = _altinnOptions.PlatformGatewayUrl + "correspondence/api/v1/" + eventSource,
            Subject = !string.IsNullOrWhiteSpace(partyId) ? "/party/" + partyId : null,
            AlternativeSubject = !string.IsNullOrWhiteSpace(alternativeSubject) ? "/organisation/" + alternativeSubject : null,
        };

        return cloudEvent;
    }
}

