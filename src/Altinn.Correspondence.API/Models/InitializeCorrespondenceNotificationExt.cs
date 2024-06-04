using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Used to specify a single notifification connected to a specific Correspondence during the Initialize Correspondence operation
    /// </summary>
    public class InitializeCorrespondenceNotificationExt
    {
        /// <summary>
        /// Which of the notifcation templates to use for this notification
        /// </summary>
        /// <remarks>
        /// Assumed valid variants:
        /// Email, SMS, EmailReminder, SMSReminder
        /// Reminders sent after 14 days if Correspondence not confirmed
        /// </remarks>
        [JsonPropertyName("notificationTemplate")]
        public required string NotificationTemplate { get; set; }

        /// <summary>
        /// Single custom text token that can be inserted into the notification template
        /// </summary>
        [JsonPropertyName("customTextToken")]
        [StringLength(128, MinimumLength = 0)]
        public string? CustomTextToken { get; set; }

        /// <summary>
        /// Senders Reference for this notification
        /// </summary>
        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }

        /// <summary>
        /// The date and time for when the notification should be sent.
        /// </summary>
        [JsonPropertyName("requestedSendTime")]
        public DateTimeOffset RequestedSendTime { get; set; }
    }
}
