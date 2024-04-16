namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Represents the important statuses for an Correspondence
    /// </summary>
    public enum CorrespondenceStatusExt : int
    {
        /// <summary>
        /// Message has been Initialized.
        /// </summary>
        Initialized = 0,

        /// <summary>
        /// Message has been Published, and is available for recipient.
        /// </summary>
        Published = 1,

        /// <summary>
        /// Message read by recipient.
        /// </summary>
        Read = 2,

        /// <summary>
        /// Recipient has replied on message.
        /// </summary>
        Replied = 3,

        /// <summary>
        /// Message confirmed by recipient.
        /// </summary>
        Confirmed = 4,

        /// <summary>
        /// Message has been deleted by recipient.
        /// </summary>
        DeletedByRecipient = 5,

        /// <summary>
        /// Message has been deleted by Altinn.
        /// </summary>
        DeletedByAltinn = 6,

        /// <summary>
        /// Message has been Archived
        /// </summary>
        Archived = 7,

        /// <summary>
        /// Recipient has opted out of digital communication in KRR
        /// </summary>
        Reserved = 8,

        /// <summary>
        /// Message has Failed
        /// </summary>
        Failed = 9
    }
}
