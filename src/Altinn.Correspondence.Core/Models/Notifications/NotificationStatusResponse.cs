using System.Text.Json.Serialization;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Notifications;

public class NotificationStatusResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }

    [JsonPropertyName("requestedSendTime")]
    public DateTime RequestedSendTime { get; set; }

    [JsonPropertyName("creator")]
    public string Creator { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("isReminder")]
    public bool IsReminder { get; set; }

    [JsonPropertyName("notificationChannel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationChannel NotificationChannel { get; set; }

    [JsonPropertyName("ignoreReservation")]
    public bool? IgnoreReservation { get; set; }

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    [JsonPropertyName("processingStatus")]
    public StatusExt ProcessingStatus { get; set; } = new();

    [JsonPropertyName("notificationsStatusSummary")]
    public NotificationsStatusDetails? NotificationsStatusDetails { get; set; }
}