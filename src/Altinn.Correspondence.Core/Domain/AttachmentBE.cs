using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// The AttachmentBE contains the information about an Attachment.
    /// </summary>
    public class AttachmentBE
    {
        #region Data contract members

        /// <summary>
        /// Gets or sets A unique identifier for an Attachment
        /// </summary>
        public int AttachmentID { get; set; }

        /// <summary>
        /// Gets or sets A logical name for the Attachment
        /// </summary>
        public string AttachmentName { get; set; }

        /// <summary>
        /// Gets or sets String containing the name of the Attachment file
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets String containing the internal name of the Attachment file (i.e. used as reference in ZIP-files)
        /// </summary>
        public string InternalFileName { get; set; }

        /// <summary>
        /// Gets or sets String representing the physical file path of the attachment
        /// </summary>
        public string PhysicalFilePath { get; set; }

        /// <summary>
        /// Gets or sets The Attachment data to be inserted into the Attachment table
        /// </summary>
        public byte[] AttachmentData { get; set; }

        /// <summary>
        /// Gets or sets The date on which this attachment is created
        /// </summary>
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Gets or sets Reference of the Sender
        /// </summary>
        public string SendersReference { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Attachment is encrypted or not
        /// </summary>
        public bool IsEncrypted { get; set; }

        /// <summary>
        /// Gets or sets attachment type ID
        /// </summary>
        public AttachmentType AttachmentTypeID { get; set; }

        /// <summary>
        /// Gets or sets attachment function type ID
        /// </summary>
        public AttachmentFunctionType AttachmentFunctionTypeID { get; set; }

        /// <summary>
        /// Gets or sets The unique identifier for a ReporteeElement
        /// </summary>
        public int ReporteeElementID { get; set; }

        /// <summary>
        /// Gets or sets The ID of the User who has added the attachment
        /// </summary>
        public int CreatedByUserID { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether attachment is added after Form Filling
        /// </summary>
        public bool IsAddedAfterFormFillin { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether attachment is included in the formset
        /// </summary>
        public bool IsAssociatedToFormSet { get; set; }

        /// <summary>
        /// Gets or sets The destination type for display for the binary attachment
        /// </summary>
        public UserTypeRestriction DestinationType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether each of the forms is locked for signing or not for user
        /// </summary>
        public bool? SigningLocked { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether each of the forms should be checked or unchecked by default
        /// in signing page
        /// </summary>
        public bool? SignedByDefault { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this attachment is deleted or not.
        /// </summary>
        public bool? IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets File size of the file
        /// </summary>
        public int FileSize { get; set; }

        /// <summary>
        /// Gets or sets The ID of attachment type to which the attachment is validated against
        /// </summary>
        public int? AttachmentRuleTypeID { get; set; }

        /// <summary>
        /// Gets or sets The name of the attachment type to which the attachment is validated against
        /// </summary>
        public string AttachmentTypeName { get; set; }

        #endregion
    }
    
    /// <summary>
    /// The AttachmentBE inherits List&lt;AttachmentBE&gt; and contains a number of AttachmentBE objects.
    /// In addition to the objects themselves, it contains the property LimitReached, which will tell if the result set
    /// size exceeded the maximum number of elements.
    /// </summary>
    public class AttachmentBEList : List<AttachmentBE>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the query returned more rows than specified as the maximum number of rows. The user should narrow the search.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}