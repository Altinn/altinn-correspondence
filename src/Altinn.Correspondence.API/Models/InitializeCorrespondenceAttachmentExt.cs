using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents an attachment to a specific correspondence as part of Initialize Correpondence Operation
    /// </summary>
    public class InitializeCorrespondenceAttachmentExt
    {
        /// <summary>
        /// A list over the Correspondence Service ResourceIds that are allowed to use this attachment data
        /// </summary>
        /// <remarks>
        /// TODO: Find a better/more generic restriction
        /// </remarks>
        [JsonPropertyName("availableForResourceIds")]
        [Required]
        public required List<string> AvailableForResourceIds { get; set; }

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
        /// The attachment data type
        /// </summary>
        [JsonPropertyName("attachmentType")]
        public AttachmentDataTypeExt AttachmentType { get; set; }

        /// <summary>
        /// Specifies the location type of the attachment data
        /// </summary>
        [JsonPropertyName("attachmentDataLocationType")]
        public required InitializeAttachmentDataLocationTypeExt AttachmentDataLocationType { get; set; }

        /// <summary>
        /// Specifies the location of the attachment data
        /// </summary>
        /// <remarks>
        /// Only required if AttachmentDataLocationType is ExistingCorrespondenceAttachment or ExisitingExternalStorage
        /// If the type is NewCorrespondenceAttachmentBlob, this requires the attachmen data to be uploaded using the AttachmentId returned from the Initialize operation
        /// </remarks>
        [JsonPropertyName("attachmentDataLocation")]
        public string? AttachmentDataLocation { get; set; }
    }
}