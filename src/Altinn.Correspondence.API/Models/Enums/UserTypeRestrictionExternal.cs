namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines where an attachment should be available.
    /// </summary>
    public enum UserTypeRestrictionExternal : int
    {
        /// <summary>
        /// Specifies default value.
        /// </summary>
        Default = 0, 

        /// <summary>
        /// Specifies that attachment should only be available in Altinn web portal.
        /// </summary>
        PortalOnly = 1, 

        /// <summary>
        /// Specifies that attachment should only be available from an end user system.
        /// </summary>
        EndUserSystemOnly = 2, 

        /// <summary>
        /// Specifies that attachment should be available from all locations.
        /// </summary>
        ShowToAll = 3
    }
}