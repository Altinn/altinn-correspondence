using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents an overview of a shared attachment that can be used by multiple correspondences
    /// </summary>
    public class AttachmentOverviewExt : InitializeAttachmentExt
    {
        /// <summary>
        /// Unique Id for this attachment
        /// </summary>
        [JsonPropertyName("attachmentId")]
        public required Guid AttachmentId { get; set; }

        /// <summary>
        /// Current attachment status
        /// </summary>
        [JsonPropertyName("status")]
        public required AttachmentStatusExt Status { get; set; }

        /// <summary>
        /// Current attachment status text description
        /// </summary>
        [JsonPropertyName("statusText")]
        public required string StatusText { get; set; }

        /// <summary>
        /// Timestamp for when the Current Attachment Status was changed
        /// </summary>
        [JsonPropertyName("statusChanged")]
        public required DateTimeOffset StatusChanged { get; set; }

        /// <summary>
        /// List of correspondences that are using this attachment
        /// </summary>
        [JsonPropertyName("correspondenceIds")]
        public required List<Guid> CorrespondenceIds { get; set; }

        /// <summary>
        /// The attachment data type in MIME format
        /// </summary>
        [JsonPropertyName("dataType")]
        public string DataType { get; set; }
    }
}