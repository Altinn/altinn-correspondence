using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    public class LegacyCorrespondenceItemExt
    {
        /// <summary>
        /// Unique Id for this correspondence
        /// </summary>
        [JsonPropertyName("correspondenceId")]
        public required Guid CorrespondenceId { get; set; }

        /// <summary>
        /// Altinn 2 Id for this correspondence if migrated from Altinn 2
        /// </summary>
        [JsonPropertyName("altinn2CorrespondenceId")]
        public required int Altinn2CorrespondenceId { get; set; }

        /// <summary>
        /// Correspondence message title/Subject.
        /// </summary>
        [JsonPropertyName("messageTitle")]
        public required string MessageTitle { get; set; }

        /// <summary>
        /// Human readable Service Owner Name.
        /// </summary>
        [JsonPropertyName("serviceOwnerName")]
        public required string ServiceOwnerName { get; set; }

        /// <summary>
        /// Current status of the Correspondence
        /// </summary>
        [JsonPropertyName("status")]
        public CorrespondenceStatusExt Status { get; set; }

        /// <summary>
        /// The minumum authentication level required to view this correspondence
        /// </summary>
        [JsonPropertyName("minimumAuthenticationlevel")]
        public required int MinimumAuthenticationLevel { get; set; }
    }
}
