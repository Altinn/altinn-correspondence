using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Brreg
{
    /// <summary>
    /// Model for sub-organization details from Brønnøysundregistrene
    /// </summary>
    public class SubOrganizationDetails: OrganizationDetails
    {
        /// <summary>
        /// Parent organization number
        /// </summary>
        [JsonPropertyName("overordnetEnhet")]
        public string? ParentOrganizationNumber { get; set; }
    }
}