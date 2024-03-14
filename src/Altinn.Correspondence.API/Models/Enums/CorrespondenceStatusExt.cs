namespace Altinn.Correspondence.API.Models.Enums
{
    public enum CorrespondenceStatusExt : int
    {
        /// <summary>
        /// Message has been Initialized.
        /// </summary>
        Initialized = 0,

        /// <summary>
        /// Message has been Published, and is available for recipients.
        /// </summary>
        Published = 1,

        /// <summary>
        /// Message read by user.
        /// </summary>
        Read = 2,

        /// <summary>
        /// User replied on message.
        /// </summary>
        Replied = 3,

        /// <summary>
        /// Message has been changed by sender
        /// </summary>
        ChangedBySender = 4,

        /// <summary>
        /// Message has been changed by recipient.
        /// </summary>
        ChangedByRecipient = 5,

        /// <summary>
        /// Message confirmed by recipient.
        /// </summary>
        Confirmed = 6,

        /// <summary>
        /// Message has been deleted by recipient.
        /// </summary>
        DeletedByRecipient = 7,

        /// <summary>
        /// Message has been deleted by Altinn.
        /// </summary>
        DeletedByAltinn = 8,

        /// <summary>
        /// Message has been Archived
        /// </summary>
        Archived = 9,

        /// <summary>
        /// User has opted out of digital communication
        /// </summary>
        Reserved = 10,

        /// <summary>
        /// Message has been Marked unread
        /// </summary>
        MarkedUnRead = 11
    }
}
