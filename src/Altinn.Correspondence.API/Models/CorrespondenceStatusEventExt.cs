using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An entity representing a Correspondence Status Event
    /// </summary>
    public class CorrespondenceStatusEventExt
    {
        /// <summary>
        /// Correspondence Status Event
        /// </summary>
        [JsonPropertyName("status")]
        public CorrespondenceStatusExt Status { get; set; }

        /// <summary>
        /// Correspondence Status Text description
        /// </summary>
        [JsonPropertyName("statusText")]
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp for when this Correspondence Status Event occurred
        /// </summary>
        [JsonPropertyName("statusChanged")]
        public DateTimeOffset StatusChanged { get; set; }
    }
}
