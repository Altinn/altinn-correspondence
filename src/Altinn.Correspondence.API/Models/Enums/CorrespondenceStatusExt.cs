namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Represents the important statuses for an Correspondence
    /// </summary>
    public enum CorrespondenceStatusExt : int
    {
        /// <summary>
        /// Correspondence has been Initialized.
        /// </summary>
        Initialized = 0,

        ///<summary>
        /// Correspondence is ready for publish, but not available for recipient.
        ///</summary>
        ReadyForPublish = 1,

        /// <summary>
        /// Correspondence has been Published, and is available for recipient.
        /// </summary>
        Published = 2,

        /// <summary>
        /// Correspondence fetched by recipient.
        /// </summary>
        Fetched = 3,

        /// <summary>
        /// Correspondence read by recipient.
        /// </summary>
        Read = 4,

        /// <summary>
        /// Recipient has replied to the Correspondence.
        /// </summary>
        Replied = 5,

        /// <summary>
        /// Correspondence has been confirmed by recipient.
        /// </summary>
        Confirmed = 6,

        /// <summary>
        /// Correspondence has been purged by recipient.
        /// </summary>
        PurgedByRecipient = 7,
        /// <summary>
        /// Correspondence has been purged by Altinn.
        /// </summary>
        PurgedByAltinn = 8,

        /// <summary>
        /// Correspondence has been Archived
        /// </summary>
        Archived = 9,

        /// <summary>
        /// Recipient has opted out of digital communication in KRR
        /// </summary>
        Reserved = 10,

        /// <summary>
        /// Correspondence has failed during initialization or processing
        /// </summary>
        Failed = 11
    }
}
