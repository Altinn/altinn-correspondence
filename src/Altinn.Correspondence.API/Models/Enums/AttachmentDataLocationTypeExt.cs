namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines the location of the attachment data
    /// </summary>
    public enum AttachmentDataLocationTypeExt : int
    {
        /// <summary>
        /// Specifies that the attachment data will need to be uploaded to Altinn Correspondence via the Upload operation
        /// </summary>
        AltinnCorrespondenceAttachmentBlob = 0,

        /// <summary>
        /// Specifies that the attachment data already exist in an external storage
        /// </summary>
        ExternalStorage = 1
    }
}