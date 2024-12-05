using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// An abstract class representing a status overview of a notification channels
/// </summary>
public class NotificationDetailsExt
{
    /// <summary>
    /// The notification id
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Boolean indicating if the sending of the notification was successful
    /// </summary>
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }

    /// <summary>
    /// The recipient of the notification
    /// </summary>
    [JsonPropertyName("recipient")]
    public NotificationRecipientExt Recipient { get; set; } = new();

    /// <summary>
    /// The result status of the notification
    /// </summary>
    [JsonPropertyName("sendStatus")]
    public NotificationStatusExt SendStatus { get; set; } = new();
}