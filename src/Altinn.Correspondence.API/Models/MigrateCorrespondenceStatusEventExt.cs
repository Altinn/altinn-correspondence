using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    public class MigrateCorrespondenceStatusEventExt 
    {
        /// <summary>
        /// Correspondence Status Event
        /// </summary>
        [JsonPropertyName("status")]
        public MigrateCorrespondenceStatusExt Status { get; set; }

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

        [JsonPropertyName("eventUserUuid")]
        public Guid? EventUserUuid { get; set; }

        [JsonPropertyName("eventUserPartyUuid")]
        public required Guid EventUserPartyUuid { get; set; }
    }
}