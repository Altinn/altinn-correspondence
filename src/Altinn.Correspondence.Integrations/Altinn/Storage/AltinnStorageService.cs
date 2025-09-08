using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Integrations.Altinn.Storage;

public class AltinnStorageService : IAltinnStorageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnStorageService> _logger;

    public AltinnStorageService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnStorageService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> AddPartyToSblBridge(int partyId, CancellationToken cancellationToken)
    {
        if (partyId <= 0)
        {
            return false;
        }
        using var response = await _httpClient.PostAsJsonAsync($"storage/api/v1/sblbridge/correspondencerecipient?partyId={partyId}", new SblBridgeParty()
        {
            PartyId = partyId
        }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Error when adding party to SBL Bridge through Storage. Statuscode was: {statusCode}, error was: {errorContent}");
        }
        return true;
    }

    public async Task<bool> SyncCorrespondenceEventToSblBridge(int altinn2CorrespondenceId, int partyId, DateTimeOffset utcEventTimestamp, SyncEventType eventType, CancellationToken cancellationToken)
    {
        if (partyId <= 0 || altinn2CorrespondenceId <= 0 || utcEventTimestamp == DateTimeOffset.MinValue)
        {
            _logger.LogWarning("Skipping SBL sync due to invalid input. altinn2Id: {Altinn2Id}, partyId: {PartyId}, ts: {Timestamp}", altinn2CorrespondenceId, partyId, utcEventTimestamp);
            return false;
        }
        using var response = await _httpClient.PostAsJsonAsync($"storage/api/v1/sblbridge/synccorrespondenceevent", new SyncCorrespondenceEvent()
        {
            PartyId = partyId,
            CorrespondenceId = altinn2CorrespondenceId,
            EventTimeStamp = utcEventTimestamp,
            EventType = eventType.ToString().ToLowerInvariant()
        }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Error when syncing Correspondence Event {eventType} for Altinn2 CorrespondenceId {altinn2CorrespondenceId} to SBL Bridge through Storage. Activating party: {partyId}. Event UTC timestamp: {utcEventTimestamp}. Status code: {statusCode}, error: {errorContent}");
        }

        return true;
    }
}
