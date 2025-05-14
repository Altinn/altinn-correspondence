using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// A more detailed object representing all the details for a correspondence, including status history and notifications
    /// </summary>
    public class CorrespondenceDetailsExt : CorrespondenceOverviewExt
    {
        /// <summary>
        /// The Status history for the Correspondence
        /// </summary>
        [JsonPropertyName("statusHistory")]
        public List<CorrespondenceStatusEventExt> StatusHistory { get; set; } = new List<CorrespondenceStatusEventExt>();

        /// <summary>
        /// Notifications directly related to this Correspondence.
        /// </summary>
        [JsonPropertyName("notifications")]
        public new List<NotificationExt>? Notifications { get; set; } = new List<NotificationExt>();

        /// <summary>
        /// Notification status for the Correspondence.
        /// </summary>
        public List<NotificationExtV2>? NotificationStatus { get; set; } = new List<NotificationExtV2>();
    }
}
