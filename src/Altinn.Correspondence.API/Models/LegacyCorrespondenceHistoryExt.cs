using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a Correspondence History Event for use by the Legacy implementation
    /// </summary>
    public class LegacyCorrespondenceHistoryExt
    {
        /// <summary>
        /// Correspondence Status Event
        /// </summary>
        [JsonPropertyName("status")]
        public required string Status { get; set; }

        /// <summary>
        /// Timestamp for when this Correspondence Status Event occurred
        /// </summary>
        [JsonPropertyName("statusChanged")]
        public DateTimeOffset? StatusChanged { get; set; }

        /// <summary>
        /// Correspondence Status Text description
        /// </summary>
        [JsonPropertyName("statusText")]
        public required string StatusText { get; set; }

        /// <summary>
        /// The user that performed the action
        /// </summary>
        [JsonPropertyName("user")]
        public required LegacyUserExt User { get; set; }

        /// <summary>
        /// Notification details if the event was notification
        /// </summary>
        [JsonPropertyName("notification")]
        public LegacyNotificationExt? Notification { get; set; }

        /// <summary>
        /// Forwarding details if the event was forwarding
        /// </summary>
        [JsonPropertyName("forwardingAction")]
        public LegacyCorrespondenceForwardingEventExt? ForwardingAction { get; set; }
    }
}
