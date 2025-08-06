using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

public class SyncCorrespondenceNotificationEventRequestExt
{
    [JsonPropertyName("correspondenceId")]
    public required Guid CorrespondenceId { get; set; }
    [JsonPropertyName("syncedNotification")]
    public required MigrateCorrespondenceNotificationExt SyncedEvent { get; set; }
}
