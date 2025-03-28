using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An entity representing a Attachment Status Event
    /// </summary>
    public class AttachmentStatusEvent
    {
        /// <summary>
        /// The attachment status
        /// </summary>
        [JsonPropertyName("status")]
        public AttachmentStatusExt Status { get; set; }

        /// <summary>
        /// Attachment status text description
        /// </summary>
        [JsonPropertyName("statusText")]
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp for when the Attachment Status occurred
        /// </summary>
        [JsonPropertyName("statusChanged")]
        public DateTimeOffset StatusChanged { get; set; }
    }
}
