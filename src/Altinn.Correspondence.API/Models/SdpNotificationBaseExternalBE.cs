namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Contains all settings for notification of digital letter via mailbox suppliers notification system.
    /// </summary>
    public class SdpNotificationBaseExternalBE
    {
        /// <summary>
        /// Gets or sets the text that is to be used for notification of the digital letter.
        /// </summary>
        public required string NotificationText { get; set; }

        /// <summary>
        /// Gets or sets a list containing number of days after the letter has been received,
        /// that notification(s) should be sent.
        /// </summary>
        public required DelayedDaysExternal Repetitions { get; set; }
    }

    /// <summary>
    /// Wraps a list of integers for better readability in SOAP contract. Used to hold number of days after the letter is sent that the user should get a notification.
    /// </summary>
    public class DelayedDaysExternal : List<uint> 
    { 
    }
}
