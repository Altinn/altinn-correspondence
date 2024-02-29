namespace Altinn.Correspondence.Core.Domain.Models.Enums
{
    /// <summary>
    ///  Correspondence Status Type is used to specify the current status of the correspondence.
    /// </summary>
    public enum CorrespondenceStatusType : int
    {
        /// <summary>
        /// Message has been created.
        /// </summary>
        Default = 0, 

        /// <summary>
        /// Message has been created.
        /// </summary>
        Created = 1, 

        /// <summary>
        /// Message read by user.
        /// </summary>
        Read = 2, 

        /// <summary>
        /// User replied on message.
        /// </summary>
        Replied = 3, 

        /// <summary>
        /// Message has been changed by government agency.
        /// </summary>
        ChangedByGovAgency = 4, 

        /// <summary>
        /// Message has been changed by user.
        /// </summary>
        ChangedByUser = 5, 

        /// <summary>
        /// Message confirmed by user.
        /// </summary>
        Confirmed = 6, 

        /// <summary>
        /// Message has been deleted by user.
        /// </summary>
        DeletedByUser = 7, 

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