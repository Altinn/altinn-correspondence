using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

public class SyncCorrespondenceForwardingEventRequestExt
{
    [JsonPropertyName("correspondenceId")]
    public required Guid CorrespondenceId { get; set; }
    [JsonPropertyName("syncedForwardingEvent")]
    public required MigrateCorrespondenceForwardingEventExt SyncedEvent { get; set; }
}
