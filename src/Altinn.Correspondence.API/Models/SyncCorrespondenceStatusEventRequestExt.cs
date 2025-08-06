using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// Request model for synchronizing correspondence status events from Altinn 2
/// </summary>
public class SyncCorrespondenceStatusEventRequestExt
{
    [JsonPropertyName("correspondenceId")]
    public required Guid CorrespondenceId { get; set; }
    [JsonPropertyName("syncedEvent")]
    public required MigrateCorrespondenceStatusEventExt SyncedEvent { get; set; }
}
