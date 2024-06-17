using Altinn.Correspondence.Core.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Application.UploadAttachment
{
    public class UploadAttachmentResponse
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
        public AttachmentStatus Status { get; set; }

        /// <summary>
        /// Current attachment status text description
        /// </summary>
        [JsonPropertyName("statusText")]
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp for when the Current Attachment Status was changed
        /// </summary>
        [JsonPropertyName("statusChanged")]
        public DateTimeOffset StatusChanged { get; set; }
    }
}
