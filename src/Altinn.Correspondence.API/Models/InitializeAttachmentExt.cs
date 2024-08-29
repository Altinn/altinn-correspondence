using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a container object for attachments used when initiatating a shared attachment
    /// </summary>
    public class InitializeAttachmentExt : BaseAttachmentExt
    {
        /// <summary>
        /// Gets or sets the Resource Id for the correspondence service.
        /// </summary>
        [JsonPropertyName("resourceId")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string ResourceId { get; set; }

        /// <summary>
        /// The Sending organisation of the correspondence. 
        /// </summary>
        /// <remarks>
        /// Organization number in countrycode:organizationnumber format.
        /// </remarks>
        [JsonPropertyName("sender")]
        [RegularExpression(@"^\d{4}:\d{9}$", ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public required string Sender { get; set; }
    }
}