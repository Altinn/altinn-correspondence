
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// A class representing a a recipient of a notification
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class NotificationRecipientOverrideExt
{
    /// <summary>
    /// recipient
    /// </summary>
    [JsonPropertyName("recipientToOverride")]
    public string RecipientToOverride { get; set; }

    /// <summary>
    /// the email address of the recipient
    /// </summary>
    [JsonPropertyName("notificationRecipient")]
    public List<NotificationRecipientExt>? NotifificationRecipient { get; set; }
}