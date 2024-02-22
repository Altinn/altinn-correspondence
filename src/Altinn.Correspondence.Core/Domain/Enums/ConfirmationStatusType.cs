namespace Altinn.Correspondence.Core.Models.Enums
{   
    /// <summary>
    ///  Enum types for denoting the different types of Confirmation statuses on a Correspondence
    /// </summary>
    public enum ConfirmationStatusType : int
    {
        /// <summary>
        /// Denotes that the correspondence has been read, but that a confirmation is still required.
        /// </summary>
        OpenedConfirmationNeeded = 1,

        /// <summary>
        /// Denotes that the correspondence has been read. A confirmation is not expected.
        /// </summary>
        OpenedNoConfirmationNeeded = 2,

        /// <summary>
        /// Denotes that the correspondence has been confirmed.
        /// </summary>
        Confirmed = 3
    }
}
