namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines the location of the attachment data during the Initialize Correspondence Operation
    /// </summary>
    public enum InitializeAttachmentDataLocationTypeExt : int
    {
        /// <summary>
        /// Specifies that the attachment data will need to be uploaded to Altinn Correspondence via the Upload Attachment operation
        /// </summary>
        NewCorrespondenceAttachmentBlob = 0,
        
        /// <summary>
        /// Specifies that the attachment  already exist in Altinn Correponddence blob storage
        /// </summary>
        ExistingCorrespondenceAttachmentBlob = 1,

        /// <summary>
        /// Specifies that the attachment data already exist in an external storage
        /// </summary>
        ExisitingExternalStorage = 2
    }
}