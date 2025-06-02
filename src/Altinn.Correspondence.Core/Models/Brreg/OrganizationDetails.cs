using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Brreg
{
    /// <summary>
    /// Model for organization details from Brønnøysundregistrene
    /// </summary>
    public class OrganizationDetails
    {
        /// <summary>
        /// Organization number
        /// </summary>
        [JsonPropertyName("organisasjonsnummer")]
        public string? OrganizationNumber { get; set; }

        /// <summary>
        /// Organization name
        /// </summary>
        [JsonPropertyName("navn")]
        public string? Name { get; set; }

        /// <summary>
        /// Whether the organization is in bankruptcy
        /// </summary>
        [JsonPropertyName("konkurs")]
        public bool IsBankrupt { get; set; }

        /// <summary>
        /// Date when the organization was deleted, null if not deleted
        /// </summary>
        [JsonPropertyName("slettedato")]
        public DateTime? DeletionDate { get; set; }

        /// <summary>
        /// Whether the organization is deleted
        /// </summary>
        [JsonIgnore]
        public bool IsDeleted => DeletionDate.HasValue;
    }
} 