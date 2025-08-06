using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

public class SyncCorrespondenceNotificationEventRequestExt
{
    [JsonPropertyName("correspondenceId")]
    public Guid CorrespondenceId { get; set; }
    [JsonPropertyName("syncedNotification")]
    public MigrateCorrespondenceNotificationExt SyncedEvent { get; set; }
}
