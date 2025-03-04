using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Used to specify a single notification connected to a specific Correspondence during the Initialize Correspondence operation
    /// </summary>
    public class InitializeCorrespondenceNotificationExt
    {
        /// <summary>
        /// Which of the notification templates to use for this notification
        /// </summary>
        [JsonPropertyName("notificationTemplate")]
        public required NotificationTemplateExt NotificationTemplate { get; set; }

        /// <summary>
        /// The emais subject for the main notification
        /// </summary>
        [JsonPropertyName("emailSubject")]
        [StringLength(128, MinimumLength = 0)]
        public string? EmailSubject { get; set; }

        /// <summary>
        /// The email body for the main notification
        /// </summary>
        [JsonPropertyName("emailBody")]
        [StringLength(1024, MinimumLength = 0)]
        public string? EmailBody { get; set; }

        /// <summary>
        /// The sms body for the main notification
        /// </summary>
        [JsonPropertyName("smsBody")]
        [StringLength(160, MinimumLength = 0)]
        public string? SmsBody { get; set; }

        /// <summary>
        /// Should a reminder be sent if the notification is not confirmed or opened
        /// </summary>
        [JsonPropertyName("sendReminder")]
        public bool SendReminder { get; set; }

        /// <summary>
        /// The email subject to use for the reminder notification
        /// </summary>
        [JsonPropertyName("reminderEmailSubject")]
        [StringLength(128, MinimumLength = 0)]
        public string? ReminderEmailSubject { get; set; }

        /// <summary>
        /// The email body to use for the reminder notification
        /// </summary>
        [JsonPropertyName("reminderEmailBody")]
        [StringLength(1024, MinimumLength = 0)]
        public string? ReminderEmailBody { get; set; }

        /// <summary>
        /// The sms body to use for the reminder notification
        /// </summary>
        [JsonPropertyName("reminderSmsBody")]
        [StringLength(160, MinimumLength = 0)]
        public string? ReminderSmsBody { get; set; }

        /// <summary>
        /// Specifies the notification channel to use for the main notification
        /// </summary>
        public NotificationChannelExt NotificationChannel { get; set; }

        /// <summary>
        ///  Specifies the notification channel to use for the reminder notification
        /// </summary>
        public NotificationChannelExt? ReminderNotificationChannel { get; set; }

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

        /// <summary>
        /// A list of recipients for the notification. If not set, the notification will be sent to the recipient of the Correspondence
        /// </summary>
        [JsonPropertyName("customNotificationRecipients")]
        public List<CustomNotificationRecipientExt>? CustomNotificationRecipients { get; set; }
    }
}
