using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An entity representing a Notificaion Status Event
    /// </summary>
    public class NotificationStatusEventExt
    {
        /// <summary>
        /// Notification Status code
        /// </summary>
        [JsonPropertyName("status")]
        public required string Status { get; set; }

        /// <summary>
        /// Notification Status Text description
        /// </summary>
        [JsonPropertyName("statusText")]
        public required string StatusText { get; set; }

        /// <summary>
        /// Timestamp for when this Correspondence Status Event occurred
        /// </summary>
        [JsonPropertyName("statusChanged")]
        public required DateTimeOffset StatusChanged { get; set; }
    }
}
