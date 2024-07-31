using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Altinn.Events.Helpers;
using Altinn.Correspondence.Repositories;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Integrations.Altinn.Events;
public class AltinnEventBus : IEventBus
{
    private readonly AltinnOptions _altinnOptions;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnEventBus> _logger;
    private readonly IResourceRightsService _altinnResourceRightsService;
    private readonly IAltinnRegisterService _altinnRegisterService;

    public AltinnEventBus(HttpClient httpClient, IAltinnRegisterService altinnRegisterService, IResourceRightsService altinnResourceRightsService, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnEventBus> logger)
    {
        _httpClient = httpClient;
        _altinnOptions = altinnOptions.Value;
        _altinnResourceRightsService = altinnResourceRightsService;
        _altinnRegisterService = altinnRegisterService;
        _logger = logger;
    }

    public async Task Publish(AltinnEventType type, string resourceId, string itemId, string eventSource, string? recipientId, CancellationToken cancellationToken = default)
    {
        string? partyId = null;
        if (recipientId != null)
        {
            partyId = await _altinnRegisterService.LookUpPartyId(recipientId, cancellationToken);
        }

        var cloudEvent = CreateCloudEvent(type, resourceId, itemId, partyId, recipientId, eventSource);
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

        if (partyId == null && alternativeSubject == null)
        {
            throw new ArgumentException("Either partyId or alternativeSubject must be set");
        }
        var alternativeSubjectFormated = handleAlternativeSubject(alternativeSubject);
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
            AlternativeSubject = alternativeSubjectFormated
        };

        return cloudEvent;
    }
    private string? handleAlternativeSubject(string? alternativeSubject)
    {
        if (alternativeSubject == null) return null;
        var organizationWithoutPrefixFormat = new Regex(@"^\d{9}$");
        var personFormat = new Regex(@"^\d{11}$");
        if (organizationWithoutPrefixFormat.IsMatch(alternativeSubject))
        {
            return "/organisation/" + alternativeSubject;
        }
        else if (personFormat.IsMatch(alternativeSubject))
        {
            return "/person/" + alternativeSubject;
        }
        return null;
    }
}

