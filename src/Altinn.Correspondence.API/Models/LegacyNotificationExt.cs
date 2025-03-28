using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    public class LegacyNotificationExt
    {
        /// <summary>
        /// The email address used for sending the notification
        /// </summary>
        [JsonPropertyName("emailAddress")]
        public string? EmailAddress { get; set; }

        /// <summary>
        /// The MobileNumber used for sending the notification SMS
        /// </summary>
        [JsonPropertyName("mobileNumber")]
        public string? MobileNumber { get; set; }

        /// <summary>
        /// The organizationNumber for the recipient of the notification
        /// </summary>
        [JsonPropertyName("organizationNumber")]
        public string? OrganizationNumber { get; set; }

        /// <summary>
        /// The SSN for the recipient of the notification
        /// </summary>
        [JsonPropertyName("nationalIdentityNumber")]
        public string? NationalIdentityNumber { get; set; }
    }
}
