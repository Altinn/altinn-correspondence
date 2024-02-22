namespace Altinn.Correspondence.Core.Models.Enums
{
    /// <summary>
    /// Defines the different statuses a correspondence can have through it's lifetime.
    /// </summary>
    public enum CorrespondenceStatusTypeAgencyExternal : int
    {
        /// <summary>
        /// Specifies that the current status of the correspondence isn't important or unknown.
        /// </summary>
        NotSet = 0,
        
        /// <summary>
        /// Specifies that the correspondence has been created.
        /// </summary>
        Created = 1,

        /// <summary>
        /// Specifies that the recipient has read the correspondence.
        /// </summary>
        Read = 2,

        /// <summary>
        /// Specifies that the recipient has confirmed the correspondence.
        /// </summary>
        Confirmed = 6
    }
}
