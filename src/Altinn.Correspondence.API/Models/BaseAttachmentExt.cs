using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    public class BaseAttachmentExt
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

    }
}