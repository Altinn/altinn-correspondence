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
        /// Gets or sets the date and time of when the notification order was created
        /// </summary>
        public DateTime CreatedDateTime { get; set; }
    }
}
