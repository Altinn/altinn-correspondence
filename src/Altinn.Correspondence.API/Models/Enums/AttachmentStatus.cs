namespace Altinn.Correspondence.API.Models.Enums
{
    public enum AttachmentStatusExt
    {
        /// <summary>
        /// Attachment has been Initialized.
        /// </summary>
        Initialized = 0,

        /// <summary>
        /// Awaiting processing of upload
        /// </summary>
        UploadProcessing = 1,

        // <summary>
        /// Published and available for download
        /// </summary>
        Published = 2,

        /// <summary>
        /// Deleted
        /// </summary>
        Deleted = 3,

        /// <summary>
        /// Failed
        /// </summary>
        Failed = 4
    }
}
