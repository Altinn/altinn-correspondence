namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Represents the important statuses for an attachment
    /// </summary>
    public enum AttachmentStatusExt
    {
        /// <summary>
        /// Attachment has been Initialized.
        /// </summary>
        Initialized = 0,

        /// <summary>
        /// Attachment is awaiting processing of upload
        /// </summary>
        UploadProcessing = 1,

        /// <summary>
        /// Attachment is published and available for download
        /// </summary>
        Published = 2,

        /// <summary>
        /// Attachment has been purged
        /// </summary>
        Purged = 3,

        /// <summary>
        /// Attachment has failed during processing
        /// </summary>
        Failed = 4
    }
}
