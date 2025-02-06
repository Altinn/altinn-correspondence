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
    /// Organization number or national identity number of the recipient to override with custom recipient(s)
    /// </summary>


    //ok, den består av denne
    [JsonPropertyName("recipientToOverride")]
    public required string RecipientToOverride { get; set; }

    /// <summary>
    /// List of custom recipients to override the default recipients
    /// </summary>

    
    //og av enda en liste
    [JsonPropertyName("notificationRecipient")]
    public required List<NotificationRecipientExt> Recipients { get; set; }
}