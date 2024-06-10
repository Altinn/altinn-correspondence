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
        /// Message fetched by recipient.
        /// </summary>
        Fetched = 2,

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
        /// Message has been purged by recipient.
        /// </summary>
        PurgedByRecipient = 6,

        /// <summary>
        /// Message has been purged by Altinn.
        /// </summary>
        PurgedByAltinn = 7,

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
