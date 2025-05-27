using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Brreg.Models
{
    /// <summary>
    /// Response model for the Brreg API organization details endpoint
    /// </summary>
    public class OrganizationDetailsResponse
    {
        /// <summary>
        /// Gets or sets the organization number
        /// </summary>
        [JsonPropertyName("organisasjonsnummer")]
        public string? OrganizationNumber { get; set; }

        /// <summary>
        /// Gets or sets the name of the organization
        /// </summary>
        [JsonPropertyName("navn")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the organization form
        /// </summary>
        [JsonPropertyName("organisasjonsform")]
        public OrganizationForm? OrganizationForm { get; set; }

        /// <summary>
        /// Gets or sets the registration date
        /// </summary>
        [JsonPropertyName("registreringsdatoEnhetsregisteret")]
        public DateTime? RegistrationDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the organization is under bankruptcy proceedings
        /// </summary>
        [JsonPropertyName("konkurs")]
        public bool IsBankrupt { get; set; }

        /// <summary>
        /// Gets or sets the deletion date of the organization (if it has been deleted)
        /// </summary>
        [JsonPropertyName("slettedato")]
        public DateTime? DeletionDate { get; set; }

        /// <summary>
        /// Gets a value indicating whether the organization has been deleted
        /// </summary>
        [JsonIgnore]
        public bool IsDeleted => DeletionDate.HasValue;
    }

    /// <summary>
    /// Organization form information
    /// </summary>
    public class OrganizationForm
    {
        /// <summary>
        /// Gets or sets the code of the organization form
        /// </summary>
        [JsonPropertyName("kode")]
        public string? Code { get; set; }

        /// <summary>
        /// Gets or sets the description of the organization form
        /// </summary>
        [JsonPropertyName("beskrivelse")]
        public string? Description { get; set; }
    }
} 