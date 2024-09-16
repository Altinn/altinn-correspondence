using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a notification connected to a specific correspondence
    /// </summary>
    public class CorrespondenceNotificationExt
    {
        /// <summary>
        /// The order id for this notification
        /// </summary>
        [JsonPropertyName("notificationId")]
        public Guid? NotificationId { get; set; }

        /// <summary>
        /// The Notification Order Id
        /// </summary>
        [JsonPropertyName("orderId")]
        public Guid? NotificationOrderId { get; set; }

        /// <summary>
        /// The email subject
        /// </summary>
        [JsonPropertyName("emailSubject")]
        public string EmailSubject { get; set; } = string.Empty;

        /// <summary>
        /// The email body
        /// </summary>
        [JsonPropertyName("emailBody")]
        public string EmailBody { get; set; } = string.Empty;

        /// <summary>
        /// The sms body
        /// </summary>
        [JsonPropertyName("smsBody")]
        public string SmsBody { get; set; } = string.Empty;

        /// <summary>
        /// The channel this notification used
        /// </summary>
        [JsonPropertyName("notificationChannel")]
        public NotificationChannelExt NotificationChannel { get; set; }

        /// <summary>
        /// The template used for the notification'
        /// </summary>
        [JsonPropertyName("notificationTemplate")]
        public NotificationTemplateExt NotificationTemplate { get; set; }


        /// <summary>
        /// The timestamp for when the notification order was created
        /// </summary>
        [JsonPropertyName("created")]
        public DateTimeOffset Created { get; set; }

        /// <summary>
        /// The timestamp for when the notification was/will be sent'
        /// </summary>
        [JsonPropertyName("requestedSendTime")]
        public DateTimeOffset RequestedSendTime { get; set; }

        /// <summary>
        /// Completed Status history for the Notification
        /// </summary>
        [JsonPropertyName("statusHistory")]
        public List<NotificationStatusEventExt> StatusHistory { get; set; } = new List<NotificationStatusEventExt>();

    }
}
