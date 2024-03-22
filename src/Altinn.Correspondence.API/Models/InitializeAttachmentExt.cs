using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a container object for attachments used when initiatating a shared attachment
    /// </summary>
    public class InitializeAttachmentExt
    {
        /// <summary>
        /// The name of the attachment file.
        /// </summary>
        [JsonPropertyName("fileName")]
        [StringLength(255, MinimumLength = 0)]
        public string? FileName { get; set; }

        /// <summary>
        /// A logical name on the attachment.
        /// </summary>
        [JsonPropertyName("name")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// A value indicating whether the attachment is encrypted or not.
        /// </summary>
        [JsonPropertyName("isEncrypted")]
        public bool IsEncrypted { get; set; }

        /// <summary>
        /// MD5 checksum for file data.
        /// </summary>
        [JsonPropertyName("checksum")]
        [MD5Checksum]
        public string? Checksum { get; set; } = string.Empty;

        /// <summary>
        /// A reference value given to the attachment by the creator.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        [StringLength(4096, MinimumLength = 1)]
        [Required]
        public required string SendersReference { get; set; }

        /// <summary>
        /// The attachment data type in MIME format
        /// </summary>
        [JsonPropertyName("dataType")]
        [Required]
        public required string DataType { get; set; }

        /// <summary>
        /// The intended usage for this attachment
        /// </summary>
        [JsonPropertyName("usageType")]
        [Required]
        public required IntendedPresentationTypeExt IntendedPresentation { get; set; }

        /// <summary>
        /// The name of the Restriction Policy restricting access to this element
        /// </summary>
        /// <remarks>
        /// An empty value indicates no restriction above the ones governing the correspondence referencing this attachment
        /// </remarks>
        [JsonPropertyName("restrictionName")]
        [Required]
        public string RestrictionName { get; set; } = string.Empty;

        /// <summary>
        /// Date/Time in UTC for when the attachen will expire and may be cleaned up automatically
        /// </summary>
        [JsonPropertyName("expirationTime")]
        [Required]
        public DateTimeOffset ExpirationTime { get; set; }
    }
}