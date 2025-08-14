using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// Request model for synchronizing correspondence notification events from Altinn 2
/// </summary>
public class SyncCorrespondenceNotificationEventRequestExt
{
    [JsonPropertyName("correspondenceId")]
    public required Guid CorrespondenceId { get; set; }
    [JsonPropertyName("syncedNotificationEvents")]
    [MinLength(1, ErrorMessage = "At least one notification event is required.")]
    public required List<MigrateCorrespondenceNotificationExt> SyncedEvents { get; set; }
}
