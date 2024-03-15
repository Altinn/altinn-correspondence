using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a binary attachment to a Correspondence
    /// </summary>
    public class CorrespondenceAttachmentExt
    {
        /// <summary>
        /// A unique id for the attachment.
        /// </summary>
        [JsonPropertyName("attachmentId")]
        public Guid? AttachmentId { get; set; }

        /// <summary>
        /// File name of the attachment file.
        /// </summary>
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        /// <summary>
        /// A logical name on the attachment.
        /// </summary>
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        /// <summary>
        /// A value indicating whether the attachment is encrypted or not.
        /// </summary>
        [JsonPropertyName("isEncrypted")]
        public bool IsEncrypted { get; set; }

        /// <summary>
        /// MD5 checksum for binary data.
        /// </summary>
        [JsonPropertyName("checksum")]
        [MD5Checksum]
        public string? Checksum { get; set; } = string.Empty;

        /// <summary>
        /// A reference value given to the attachment by the creator.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        public required string SendersReference { get; set; }

        /// <summary>
        /// The date on which this attachment is created
        /// </summary>
        [JsonPropertyName("createdDateTime")]
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// The attachment data / file type
        /// </summary>
        /// <remarks>
        /// TODO: Swapped out for a more generic type?
        /// </remarks>
        [JsonPropertyName("attachmentType")]
        public AttachmentDataTypeExt AttachmentType { get; set; }

        // <summary>
        /// Specifies the location of the attachment data
        /// </summary>
        [JsonPropertyName("attachmentDataLocationType")]
        public AttachmentDataLocationTypeExt AttachmentDataLocationType { get; set; }

        /// <summary>
        /// Specifies the location of the attachment data
        /// </summary>
        [JsonPropertyName("attachmentDataLocation")]
        public required string AttachmentDataLocation { get; set; }
    }
}