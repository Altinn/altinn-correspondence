namespace Altinn.Correspondence.Core.Models.Enums
{
    /// <summary>
    /// Represents the important statuses for an Correspondence
    /// </summary>
    public enum CorrespondenceStatus : int
    {
        /// <summary>
        /// Message has been Initialized.
        /// </summary>
        Initialized = 0,

        /// <summary>
        /// Message is ready for publish, but not available for recipient.
        /// </summary>
        ReadyForPublish = 1,

        /// <summary>
        /// Message has been Published, and is available for recipient.
        /// </summary>
        Published = 2,

        /// <summary>
        /// Message read by recipient.
        /// </summary>
        Read = 3,

        /// <summary>
        /// Recipient has replied on message.
        /// </summary>
        Replied = 4,

        /// <summary>
        /// Message confirmed by recipient.
        /// </summary>
        Confirmed = 5,

        /// <summary>
        /// Message has been deleted by recipient.
        /// </summary>
        DeletedByRecipient = 6,

        /// <summary>
        /// Message has been deleted by Altinn.
        /// </summary>
        DeletedByAltinn = 7,

        /// <summary>
        /// Message has been Archived
        /// </summary>
        Archived = 8,

        /// <summary>
        /// Recipient has opted out of digital communication in KRR
        /// </summary>
        Reserved = 9,

        /// <summary>
        /// Message has Failed
        /// </summary>
        Failed = 10
    }
}
