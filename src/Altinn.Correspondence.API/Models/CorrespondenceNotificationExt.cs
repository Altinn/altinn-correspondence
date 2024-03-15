using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

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
        public Guid NotificationId { get; set; }

        /// <summary>
        /// Which of the notifcation templates to use for this notification
        /// </summary>
        [JsonPropertyName("notificationTemplate")]
        public required string NotificationTemplate { get; set; }

        /// <summary>
        /// Senders Reference for this notification
        /// </summary>
        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }

        /// <summary>
        /// The channel for this notification
        /// </summary>
        [JsonPropertyName("notificationChannel")]
        public NotificationChannelExt NotificationChannel { get; set; }

        /// <summary>
        /// The date and time for when the notification should be sent.
        /// </summary>
        [JsonPropertyName("requestedSendTime")]
        public DateTime RequestedSendTime { get; set; }

        /// <summary>
        /// Gets or sets the date and time of when the notification order was created
        /// </summary>
        public DateTime Created { get; set; }
    }
}
