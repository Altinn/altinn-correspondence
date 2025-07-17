using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Correspondence.Common.Constants;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a container object for attachments used when initiating a shared attachment
    /// </summary>
    public class InitializeAttachmentExt : BaseAttachmentExt
    {
        /// <summary>
        /// Gets or sets the Resource Id for the correspondence service.
        /// </summary>
        [JsonPropertyName("resourceId")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        [ResourceIdentifier]
        public required string ResourceId { get; set; }

        /// <summary>
        /// The Sending organisation of the correspondence. 
        /// </summary>
        /// <remarks>
        /// Organization number in countrycode:organizationnumber format.
        /// </remarks>
        [JsonPropertyName("sender")]
        [Obsolete("Sender is deprecated and will be removed in a future version. The sender is now automatically determined from the Resource Registry based on the resourceId.")]
        public string? Sender { get; set; }
    }
}