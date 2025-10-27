using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Altinn.Events.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.Integrations.Altinn.Events;
public class AltinnEventBus : IEventBus
{
    private readonly GeneralSettings _generalSettings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnEventBus> _logger;
    private readonly IAltinnRegisterService _altinnRegisterService;

    public AltinnEventBus(HttpClient httpClient, IAltinnRegisterService altinnRegisterService, IOptions<GeneralSettings> generalSettings, ILogger<AltinnEventBus> logger)
    {
        _httpClient = httpClient;
        _generalSettings = generalSettings.Value;
        _altinnRegisterService = altinnRegisterService;
        _logger = logger;
    }

    public async Task Publish(AltinnEventType type, string resourceId, string itemId, string eventSource, string? party, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Publishing cloud event {type} for resource {resourceId} with event source {eventSource} and item ID {itemId}. Recipient is {recipient}.", type, resourceId, eventSource, itemId, party);

        var cloudEvent = CreateCloudEvent(type, resourceId, itemId, party, eventSource);
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

    private CloudEvent CreateCloudEvent(AltinnEventType type, string resourceId, string itemId, string? party, string eventSource)
    {
        if (party == null)
        {
            throw new ArgumentException("Either partyId or alternativeSubject must be set");
        }
        var alternativeSubjectFormated = handleAlternativeSubject(party);
        CloudEvent cloudEvent = new CloudEvent()
        {
            Id = Guid.NewGuid(),
            SpecVersion = "1.0",
            Time = DateTimeOffset.UtcNow,
            Resource = UrnConstants.Resource + ":" + resourceId,
            ResourceInstance = itemId,
            Type = "no.altinn.correspondence." + type.ToString().ToLowerInvariant(),
            Source = _generalSettings.CorrespondenceBaseUrl.TrimEnd('/') + "/correspondence/api/v1/" + eventSource,
            Subject = party.WithUrnPrefix(),
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

