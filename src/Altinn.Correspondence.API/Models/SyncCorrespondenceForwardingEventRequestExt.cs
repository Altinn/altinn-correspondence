using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// Request model for synchronizing correspondence forwarding events from Altinn 2
/// </summary>
public class SyncCorrespondenceForwardingEventRequestExt
{
    [JsonPropertyName("correspondenceId")]
    public required Guid CorrespondenceId { get; set; }
    [JsonPropertyName("syncedForwardingEvent")]
    public required MigrateCorrespondenceForwardingEventExt SyncedEvent { get; set; }
}
