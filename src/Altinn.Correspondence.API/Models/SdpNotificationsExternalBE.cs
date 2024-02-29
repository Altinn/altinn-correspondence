namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Contains all settings for notification of digital letter via mailbox suppliers notification system.
    /// </summary>
    public class SdpNotificationsExternalBE
    {
        /// <summary>
        /// Gets or sets the details required to generate an email notification.
        /// </summary>
        public SdpEmailNotificationExternalBE EmailNotification { get; set; }

        /// <summary>
        /// Gets or sets the details required to generate a SMS notification.
        /// </summary>
        public SdpSmsNotificationExternalBE SmsNotification { get; set; }
    }
}
