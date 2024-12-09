namespace Altinn.Correspondence.Core.Models.Notifications;

public class CustomNotificationRecipient
{
    /// <summary>
    /// Which recipient we want to override the notification information for
    /// </summary>
    public string RecipientToOverride { get; set; }

    /// <summary>
    /// The new notification information for the recipient
    /// </summary>
    public List<Recipient> Recipients { get; set; }
}