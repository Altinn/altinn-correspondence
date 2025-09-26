using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    public class MigrateCorrespondenceNotificationExt
    {
        /// <summary>
        /// Where to send the notification
        /// </summary>
        [JsonPropertyName("notificationChannel")]
        public required NotificationChannelExt NotificationChannel { get; set; }

        /// <summary>
        /// The date and time for when the notification was sent.
        /// </summary>
        [JsonPropertyName("notificationSent")]        
        public required DateTimeOffset NotificationSent { get; set; }

        /// <summary>
        /// Id of the Notification in Altinn 2.
        /// </summary>
        [JsonPropertyName("altinn2NotificationId")]   
        public required int Altinn2NotificationId { get; set; }
        
        /// <summary>
        /// Senders Reference for this notification
        /// </summary>
        [JsonPropertyName("notificationAddress")]
        public required string NotificationAddress { get; set; }

        /// <summary>
        /// Whether the notification is for a Reminder notification Type.
        /// </summary>
        [JsonPropertyName("isReminder")]
        public required bool IsReminder { get; set; }
    }
}