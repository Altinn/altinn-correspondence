using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents an attachment to a specific correspondence as part of Initialize Correspondence Operation
    /// </summary>
    public class InitializeCorrespondenceAttachmentExt : BaseAttachmentExt
    {
        /// <summary>
        /// A unique id for the correspondence attachment.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Specifies the location type of the attachment data
        /// </summary>
        [JsonPropertyName("dataLocationType")]
        [Required]
        public InitializeAttachmentDataLocationTypeExt DataLocationType { get; set; }

        /// <summary>
        /// The expiration time of the attachment
        /// </summary>
        [JsonPropertyName("expirationTime")]
        public DateTimeOffset ExpirationTime { get; set; }
    }
}