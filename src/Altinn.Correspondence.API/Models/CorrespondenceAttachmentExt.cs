using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a binary attachment to a Correspondence
    /// </summary>
    public class CorrespondenceAttachmentExt : InitializeCorrespondenceAttachmentExt
    {
        /// <summary>
        /// A unique id for the attachment.
        /// </summary>
        [JsonPropertyName("attachmentId")]
        public Guid? AttachmentId { get; set; }

        /// <summary>
        /// The date on which this attachment is created
        /// </summary>
        [JsonPropertyName("createdDateTime")]
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Specifies the location of the attachment data
        /// </summary>
        [JsonPropertyName("dataLocationType")]
        public new AttachmentDataLocationTypeExt DataLocationType { get; set; }
    }
}