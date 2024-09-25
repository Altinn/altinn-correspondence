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
        public required NotificationTemplateExt NotificationTemplate { get; set; }

        /// <summary>
        /// The email template to use for this notification
        /// </summary>
        [JsonPropertyName("emailSubject")]
        [StringLength(128, MinimumLength = 0)]
        public string? EmailSubject { get; set; }

        /// <summary>
        /// The email template to use for this notification
        /// </summary>
        [JsonPropertyName("emailBody")]
        [StringLength(1024, MinimumLength = 0)]
        public string? EmailBody { get; set; }

        /// <summary>
        /// The sms template to use for this notification
        /// </summary>
        [JsonPropertyName("smsBody")]
        [StringLength(160, MinimumLength = 0)]
        public string? SmsBody { get; set; }

        /// <summary>
        /// Should a reminder be sent if the notification is not confirmed
        /// </summary>
        [JsonPropertyName("sendReminder")]
        public bool SendReminder { get; set; }

        /// <summary>
        /// The email template to use for this notification
        /// </summary>
        [JsonPropertyName("reminderEmailSubject")]
        [StringLength(128, MinimumLength = 0)]
        public string? ReminderEmailSubject { get; set; }

        /// <summary>
        /// The email template to use for this notification
        /// </summary>
        [JsonPropertyName("reminderEmailBody")]
        [StringLength(1024, MinimumLength = 0)]
        public string? ReminderEmailBody { get; set; }

        /// <summary>
        /// The sms template to use for this notification
        /// </summary>
        [JsonPropertyName("reminderSmsBody")]
        [StringLength(160, MinimumLength = 0)]
        public string? ReminderSmsBody { get; set; }

        /// <summary>
        /// Where to send the notification
        /// </summary>
        public NotificationChannelExt NotificationChannel { get; set; }

        /// <summary>
        /// Senders Reference for this notification
        /// </summary>
        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }

        /// <summary>
        /// The date and time for when the notification should be sent.
        /// </summary>
        [JsonPropertyName("requestedSendTime")]
        public DateTimeOffset? RequestedSendTime { get; set; }
    }
}
