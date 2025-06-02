namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Enum describing available notification channels.
    /// </summary>
    public enum NotificationChannelExt
    {
        /// <summary>
        /// The selected channel for the notification is only email.
        /// </summary>
        Email = 0,

        /// <summary>
        /// The selected channel for the notification is only sms.
        /// </summary>
        Sms = 1,

        /// <summary>
        /// The selected channel for the notification is email preferred.
        /// </summary>
        EmailPreferred = 2,

        /// <summary>
        /// The selected channel for the notification is SMS preferred.
        /// </summary>
        SmsPreferred = 3,

        /// <summary>
        /// The selected channel for the notification is both email and sms.
        /// </summary>
        EmailAndSms = 4,
    }
}