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
        /// The id of the notification in the correspondence system.
        /// </summary>
        [JsonPropertyName("Id")]
        public Guid? Id { get; set; }

        /// <summary>
        /// An external ID of the notification order id in Altinn Notifications
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
    }
}
