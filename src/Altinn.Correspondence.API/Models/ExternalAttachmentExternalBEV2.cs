using System;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a container object for attachments.
    /// </summary>
    public class ExternalAttachmentExternalBEV2
    {
        /// <summary>
        /// Gets or sets a strongly typed list of BinaryAttachmentV2 elements that can be accessed by index.
        /// </summary>
        public BinaryAttachmentExternalBEV2List BinaryAttachments { get; set; }

        /// <summary>
        /// Gets or sets a strongly typed list of XmlAttachmentV2 elements that can be accessed by index.
        /// </summary>
        public XmlAttachmentExternalBEV2List XmlAttachmentList { get; set; }
    }
}