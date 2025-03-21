using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a user for use by the Legacy implementation
    /// </summary>
    public class LegacyUserExt
    {
        /// <summary>
        /// The partyId of the user
        /// </summary>
        [JsonPropertyName("partyId")]
        public int? PartyId { get; set; }

        /// <summary>
        /// The SSSN of the user
        /// </summary>
        [JsonPropertyName("nationalIdentityNumber")]
        public string? NationalIdentityNumber { get; set; }

        /// <summary>
        /// The name of the user
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
