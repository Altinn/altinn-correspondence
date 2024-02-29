using System;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a container object for attachments.
    /// </summary>
    public class InitiateSharedAttachmentExt
    {
        /// <summary>
        /// A list over the Correspondence Service ResourceIds that are allowed to use this attachment data
        /// </summary>
        public List<string> AvailableForResourceIds { get; set; }

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
        /// Gets or sets a reference value given to the attachment by the creator.
        /// </summary>
        public string SendersReference { get; set; }

        /// <summary>
        /// Gets or sets Function Type
        /// </summary>
        public AttachmentFunctionTypeExternal FunctionType { get; set; }

        /// <summary>
        /// Gets or sets the attachment type
        /// </summary>
        public AttachmentTypeExternal AttachmentTypeID { get; set; }

        /// <summary>
        /// Gets or sets an indicator value for where the binary attachment can be visible.
        /// </summary>
        public UserTypeRestrictionExternal DestinationType { get; set; }
    }
}