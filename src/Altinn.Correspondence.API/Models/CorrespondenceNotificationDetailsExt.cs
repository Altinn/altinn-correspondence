using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a notification connected to a specific correspondence
    /// </summary>
    public class CorrespondenceNotificationDetailsExt : CorrespondenceNotificationOverviewExt
    {
        /// <summary>
        /// Current status for the notification
        /// </summary>        
        [JsonPropertyName("status")]
        public string Status { get; set; }

        /// <summary>
        /// Current status text for the notification
        /// </summary>
        [JsonPropertyName("statusText")]
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// When the current status changed
        /// </summary>
        [JsonPropertyName("statusChanged")]
        public DateTimeOffset StatusChanged { get; set; }

        /// <summary>
        /// Completed Status history for the Notification
        /// </summary>
        [JsonPropertyName("statusHistory")]
        public List<NotificationStatusEventExt> StatusHistory { get; set; }
    }
}
