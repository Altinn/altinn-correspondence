
using System.Text.Json.Serialization;

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
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Boolean indicating if the notification is a reminder
    /// </summary>
    [JsonPropertyName("isReminder")]
    public bool? IsReminder { get; set; }

    /// <summary>
    /// The status of the notification
    /// </summary>
    [JsonPropertyName("status")]
    public InitializedNotificationStatusExt Status { get; set; }
}
public enum InitializedNotificationStatusExt
{
    /// <summary>
    /// The recipient lookup was successful for at least one recipient
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