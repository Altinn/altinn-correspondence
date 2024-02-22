namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// This entity provides all the details pertaining to the requested Correspondence from the EUS.
    /// </summary>
    public class CorrespondenceForEndUserSystemBE
    {
        #region Data contract members

        /// <summary>
        /// Gets or sets Correspondence Identifier
        /// </summary>
        public CorrespondenceBE Correspondence { get; set; }

        /// <summary>
        /// Gets or sets the Links associated with this Correspondence
        /// </summary>
        public CorrespondenceLinkBEList CorrespondenceLinks { get; set; }

        /// <summary>
        /// Gets or sets the Attachments associated with this Correspondence
        /// </summary>
        public AttachmentBEList CorrespondenceAttachments { get; set; }

        /// <summary>
        /// Gets or sets the Notifications associated with this Correspondence
        /// </summary>
        public InternalNotificationBEList CorrespondenceNotifications { get; set; }

        #endregion
    }
}