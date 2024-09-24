namespace Altinn.Correspondence.Core.Models.Enums
{
    public enum NotificationTemplate
    {
        TextTokenOnly,
        GenericPersonMessage,
        GenericOrganizationMessage,

        /// <summary>
        /// Notification was sent from Altinn 2 and then exported to Altinn 3 along with the Altinn 2 message.
        /// </summary>
        Altinn2Message,
    }
}