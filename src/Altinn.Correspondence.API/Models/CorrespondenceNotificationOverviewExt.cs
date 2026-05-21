using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// Summary of a notification order linked to a correspondence.
/// </summary>
public class CorrespondenceNotificationOverviewExt
{
    /// <summary>
    /// The notification order identifier, when available.
    /// </summary>
    [JsonPropertyName("notificationOrderId")]
    public Guid? NotificationOrderId { get; set; }

    /// <summary>
    /// Whether the notification is a reminder.
    /// </summary>
    [JsonPropertyName("isReminder")]
    public bool IsReminder { get; set; }
}
