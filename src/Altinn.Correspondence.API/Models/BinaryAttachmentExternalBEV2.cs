using System;
using System.Collections.Generic;

using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a binary attachment. Binary attachments are any attachment, text, xml, binary, etc. where the content is
    /// ignored (irrelevant) for Altinn. 
    /// </summary>
    public class BinaryAttachmentExternalBEV2
    {
        /// <summary>
        /// Gets or sets a unique id of the attachment.
        /// </summary>
        /// <remarks>
        /// This property is not visible for external systems.
        /// </remarks>
        public int AttachmentID { get; set; }

        /// <summary>
        /// Gets or sets the name of the attachment file.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets a logical name on the attachment.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the attachment is encrypted or not.
        /// </summary>
        public bool Encrypted { get; set; }

        /// <summary>
        /// Gets or sets the content of the attachment.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Gets or sets a reference value given to the attachment by the creator.
        /// </summary>
        public string SendersReference { get; set; }

        /// <summary>
        /// Gets or sets Function Type
        /// </summary>
        public AttachmentFunctionTypeExternal FunctionType { get; set; }

        /// <summary>
        /// Gets or sets The date on which this attachment is created
        /// </summary>
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Gets or sets the attachment type
        /// </summary>
        public AttachmentTypeExternal AttachmentTypeID { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for a ReporteeElement
        /// </summary>
        public int ReporteeElementID { get; set; }

        /// <summary>
        /// Gets or sets an indicator value for where the binary attachment can be visible.
        /// </summary>
        public UserTypeRestrictionExternal DestinationType { get; set; }
    }

    /// <summary>
    /// Represents a strongly typed list of BinaryAttachmentV2 elements that can be accessed by index.
    /// </summary>
    public class BinaryAttachmentExternalBEV2List : List<BinaryAttachmentExternalBEV2>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the current list is complete or not. If this flag is true, it means that there exists more
        /// BinaryAttachmentV2 elements there were not added to the list.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}