﻿using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a binary attachment to a Correspondence
    /// </summary>
    public class CorrespondenceAttachmentOverviewExt : InitializeCorrespondenceAttachmentExt
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
        public DateTimeOffset CreatedDateTime { get; set; }

        /// <summary>
        /// Specifies the location of the attachment data
        /// </summary>
        [JsonPropertyName("dataLocationType")]
        public new AttachmentDataLocationTypeExt DataLocationType { get; set; }

        /// <summary>
        /// Current attachment status
        /// </summary>
        [JsonPropertyName("status")]
        public AttachmentStatusExt Status { get; set; }

        /// <summary>
        /// Current attachment status text description
        /// </summary>
        [JsonPropertyName("statusText")]
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp for when the Current Attachment Status was changed
        /// </summary>
        [JsonPropertyName("StatusChangedDateTime")]
        public DateTimeOffset StatusChangedDateTime { get; set; }
    }
}