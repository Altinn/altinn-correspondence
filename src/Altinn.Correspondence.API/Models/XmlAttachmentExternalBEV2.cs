using System;
using System.Collections.Generic;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents an XML attachment.
    /// </summary>
    public class XmlAttachmentExternalBEV2
    {
        /// <summary>
        /// Gets or sets a reference from the sender of the attachment.
        /// </summary>
        public string SendersReference { get; set; }

        /// <summary>
        /// Gets or sets the xml document content.
        /// </summary>
        public string FormDataXml { get; set; }

        /// <summary>
        /// Gets or sets the data format id of a form. Both this and DataFormatVersionId is needed to identify the correct form in Altinn.
        /// </summary>
        public string DataFormatId { get; set; }

        /// <summary>
        /// Gets or sets the data format version id of a form. Both this and DataFormatId is needed to identify the correct form in Altinn.
        /// </summary>
        public int DataFormatVersion { get; set; }

        /// <summary>
        /// Gets or sets the id of a form as a part of a form set.
        /// </summary>
        public int LogicalFormInFormSetID { get; set; }
    }

    /// <summary>
    /// Represents a strongly typed list of XmlAttachmentV2 elements that can be accessed by index.
    /// </summary>
    public class XmlAttachmentExternalBEV2List : List<XmlAttachmentExternalBEV2>
    {
    }
}