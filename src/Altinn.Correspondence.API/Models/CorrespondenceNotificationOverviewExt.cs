using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a notification connected to a specific correspondence
    /// </summary>
    public class CorrespondenceNotificationOverviewExt : InitializeCorrespondenceNotificationExt
    {
        /// <summary>
        /// The order id for this notification
        /// </summary>
        [JsonPropertyName("notificationId")]
        public Guid NotificationId { get; set; }

        /// <summary>
        /// The channel this notification used
        /// </summary>
        [JsonPropertyName("notificationChannel")]
        public NotificationChannelExt NotificationChannel { get; set; }

        /// <summary>
        /// The timestamp for when the notification order was created
        /// </summary>
        [JsonPropertyName("created")]
        public DateTimeOffset Created { get; set; }
    }
}
