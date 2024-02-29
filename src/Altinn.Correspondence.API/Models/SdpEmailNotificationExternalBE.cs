namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a notification with an email address as the notification recipient. This class tries to match
    /// the <a href="http://begrep.difi.no/SikkerDigitalPost/1.2.1/begrep/EpostVarsel">email notification concept</a> defined by DIFI.
    /// </summary>
    public class SdpEmailNotificationExternalBE : SdpNotificationBaseExternalBE
    {
        /// <summary>
        /// Gets or sets the email address for the recipient of the notification.
        /// </summary>
        public required string EmailAddress { get; set; }
    }
}
