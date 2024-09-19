namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Enum describing available notification channels.
    /// </summary>
    public enum NotificationChannelExt
    {
        /// <summary>
        /// The selected channel for the notification is email.
        /// </summary>
        Email,

        /// <summary>
        /// The selected channel for the notification is email preferred.
        /// </summary>
        EmailPreferred,

        /// <summary>
        /// The selected channel for the notification is sms.
        /// </summary>
        Sms,

        /// <summary>
        /// The selected channel for the notification is SMS preferred.
        /// </summary>
        SmsPreferred
    }
}