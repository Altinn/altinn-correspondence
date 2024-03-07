using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a binary attachment that may be used by the Correspondence
    /// </summary>
    public class CorrespondenceAttachmentExt
    {
        /// <summary>
        /// Gets or sets a unique id of the attachment.
        /// </summary>
        public string? AttachmentID { get; set; }

        /// <summary>
        /// Gets or sets the file name of the attachment file.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Gets or sets a logical name on the attachment.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the attachment is encrypted or not.
        /// </summary>
        public bool IsEncrypted { get; set; }

        /// <summary>
        /// Gets or sets the content of the attachment.
        /// </summary>
        public byte[]? Data { get; set; }

        /// <summary>
        /// MD5 checksum for file data.
        /// </summary>
        [JsonPropertyName("checksum")]
        [MD5Checksum]
        public string? Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a reference value given to the attachment by the creator.
        /// </summary>
        public required string SendersReference { get; set; }

        /// <summary>
        /// Gets or sets Function Type
        /// </summary>
        /// <remarks>
        /// TODO: Can be removed?
        /// </remarks>
        public AttachmentFunctionTypeExternal FunctionType { get; set; }

        /// <summary>
        /// Gets or sets The date on which this attachment is created
        /// </summary>
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Gets or sets the attachment type
        /// </summary>
        /// <remarks>
        /// TODO: Can be removed?
        /// </remarks>
        public AttachmentTypeExt AttachmentTypeID { get; set; }
    }
}