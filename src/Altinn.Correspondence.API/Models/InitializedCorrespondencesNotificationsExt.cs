
using System.Text.Json.Serialization;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.API.Models;
/// <summary>
/// Information about a notification that were created for the correspondence
/// </summary>
public class InitializedCorrespondencesNotificationsExt
{
    /// <summary>
    /// The order ID of the notification
    /// </summary>
    [JsonPropertyName("orderId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Boolean indicating if the notification is a reminder
    /// </summary>
    [JsonPropertyName("isReminder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsReminder { get; set; }

    /// <summary>
    /// The status of the notification
    /// </summary>
    [JsonPropertyName("status")]
    public InitializedNotificationStatusExt Status { get; set; }

    /// <summary>
    /// The notification channels of the notification 
    /// </summary>
    [JsonPropertyName("notificationChannels")]
    public List<string>? NotificationChannels { get; set; }

    /// <summary>
    /// The notification template of the notification
    /// </summary>
    [JsonPropertyName("notificationTemplate")]
    public NotificationTemplate? NotificationTemplate { get; set; }

    /// <summary>
    /// Boolean indicating if a reminder should be sent for the notification
    /// </summary>    
    [JsonPropertyName("sendReminder")]
    public bool SendReminder { get; set; }
}
public enum InitializedNotificationStatusExt
{
    /// <summary>
    /// The recipient lookup was successful for at least one recipient and the notification order was successful
    /// </summary>
    Success,
    /// <summary>
    /// The recipient lookup failed for all recipients
    /// </summary>
    MissingContact,
    /// <summary>
    /// The notification order failed to be created due to an error
    /// </summary>
    Failure,
}