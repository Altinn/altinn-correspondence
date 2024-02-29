namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a notification with a mobile number as the notification recipient. This class tries to match
    /// the <a href="http://begrep.difi.no/SikkerDigitalPost/1.2.1/begrep/SmsVarsel">SMS notification concept</a> defined by DIFI.
    /// </summary>
    public class SdpSmsNotificationExternalBE : SdpNotificationBaseExternalBE
    {
        /// <summary>
        /// Gets or sets the mobile number for the recipient of the notification.
        /// </summary>
        public string MobileNumber { get; set; }
    }
}