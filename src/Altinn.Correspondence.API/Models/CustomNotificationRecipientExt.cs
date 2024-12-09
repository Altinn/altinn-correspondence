using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// A class representing a a recipient of a notification
/// </summary>
/// <remarks>
/// External representation to be used in the API.
/// </remarks>
public class CustomNotificationRecipientExt
{
    /// <summary>
    /// recipient
    /// </summary>
    [JsonPropertyName("recipientToOverride")]
    public required string RecipientToOverride { get; set; }

    /// <summary>
    /// the email address of the recipient
    /// </summary>
    [JsonPropertyName("notificationRecipient")]
    public List<NotificationRecipientExt>? Recipients { get; set; }
}