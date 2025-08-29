namespace Altinn.Correspondence.Core.Models.Enums
{
    /// <summary>
    /// Represents the important statuses for an Correspondence
    /// </summary>
    public enum CorrespondenceDeleteEventType : int
    {
        /// <summary>
        /// The correspondence was hard deleted by the sender/serviceOwner/Altinn, equivalent to purge
        /// </summary>
        HardDeletedByServiceOwner = 1,
        /// <summary>
        /// The correspondence was hard deleted by the recipient, equivalent to purge
        /// </summary>
        HardDeletedByRecipient = 2,
        /// <summary>
        /// The correspondence was soft deleted by the recipient
        /// </summary>
        SoftDeletedByRecipient = 3,
        /// <summary>
        /// The correspondence was restored by the recipient from soft delete
        /// </summary>
        RestoredByRecipient = 4
    }
}
