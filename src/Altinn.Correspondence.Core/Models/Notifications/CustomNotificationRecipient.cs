namespace Altinn.Correspondence.Core.Models.Notifications;

public class CustomNotificationRecipient
{
    /// <summary>
    /// Organization number or national identity number of the recipient to override with custom recipient(s)
    /// </summary>
    public required string RecipientToOverride { get; set; }

    /// <summary>
    /// List of new recipients to override the default recipients
    /// </summary>
    public required List<Recipient> Recipients { get; set; }
}