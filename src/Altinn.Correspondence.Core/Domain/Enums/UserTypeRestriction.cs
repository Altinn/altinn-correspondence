namespace Altinn.Correspondence.Core.Models.Enums
{
    /// <summary>
    /// Enumeration for user type restriction
    /// </summary>
    public enum UserTypeRestriction : int
    {
        /// <summary>
        /// Default restriction
        /// </summary>
        Default = 0, 

        /// <summary>
        /// Portal only
        /// </summary>
        PortalOnly = 1, 

        /// <summary>
        /// End user system only
        /// </summary>
        EndUserSystemOnly = 2, 

        /// <summary>
        /// Show to All
        /// </summary>
        ShowToAll = 3
    }
}