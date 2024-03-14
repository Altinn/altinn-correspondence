using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a container object for attachments.
    /// </summary>
    public class AttachmentOverviewExt
    {
        /// <summary>
        /// Unique Id for this attachment
        /// </summary>
        [JsonPropertyName("attachmentId")]
        public required Guid AttachmentId { get; set; }

        /// <summary>
        /// A list over the Correspondence Service ResourceIds that are allowed to use this attachment data
        /// </summary>
        /// <remarks>
        /// TODO: Find a better/more generic restriction
        /// </remarks>
        public required List<string> AvailableForResourceIds { get; set; }

        /// <summary>
        /// Gets or sets the name of the attachment file.
        /// </summary>
        [JsonPropertyName("fileName")]
        [StringLength(255, MinimumLength = 0)]
        public string? FileName { get; set; }

        /// <summary>
        /// Gets or sets a logical name on the attachment.
        /// </summary>
        [JsonPropertyName("name")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the attachment is encrypted or not.
        /// </summary>
        [JsonPropertyName("isEncrypted")]
        public bool IsEncrypted { get; set; }

        /// <summary>
        /// MD5 checksum for file data.
        /// </summary>
        [JsonPropertyName("checksum")]
        public string? Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a reference value given to the attachment by the creator.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        [StringLength(4096, MinimumLength = 1)]
        [Required]
        public required string SendersReference { get; set; }

        /// <summary>
        ///The attachment data type
        /// </summary>
        [JsonPropertyName("attachmentType")]
        public AttachmentDataTypeExt AttachmentType { get; set; }

        /// <summary>
        /// Specifies the location of the attachment data
        /// </summary>
        [JsonPropertyName("attachmentDataLocation")]
        public AttachmentDataLocationTypeExt AttachmentDataLocation { get; set; }

        /// <summary>
        /// Current attachment status
        /// </summary>
        [JsonPropertyName("attachmentStatus")]
        public AttachmentStatusExt AttachmentStatus { get; set; }

        /// <summary>
        /// Current attachment status text description
        /// </summary>
        [JsonPropertyName("attachmentStatusText")]
        public string AttachmentStatusText { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp for when the Current Attachment Status was changed
        /// </summary>
        [JsonPropertyName("attachmentStatusChanged")]
        public DateTimeOffset AttachmentStatusChanged { get; set; }
    }
}