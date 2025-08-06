using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

public class SyncCorrespondenceStatusEventRequestExt
{
    [JsonPropertyName("correspondenceId")]
    public required Guid CorrespondenceId { get; set; }
    [JsonPropertyName("syncedEvent")]
    public required MigrateCorrespondenceStatusEventExt SyncedEvent { get; set; }
}
