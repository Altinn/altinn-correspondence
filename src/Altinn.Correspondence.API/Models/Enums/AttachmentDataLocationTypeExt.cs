namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines the location of the attachment data
    /// </summary>
    public enum AttachmentDataLocationTypeExt : int
    {
        /// <summary>
        /// Specifies that the attachment data is stored in the Altinn Correspondence Storage
        /// </summary>
        AltinnCorrespondenceAttachment = 0,

        /// <summary>
        /// Specifies that the attachment data is stored in an external storage controlled by the sender
        /// </summary>
        ExternalStorage = 1
    }
}