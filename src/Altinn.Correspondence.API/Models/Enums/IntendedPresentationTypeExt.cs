namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines intended presentation for attachment
    /// </summary>
    public enum IntendedPresentationTypeExt : int
    {
        /// <summary>
        /// Human-readable content to be displayed in GUI, such as Message Body
        /// </summary>
        HumanReadable = 0, 

        /// <summary>
        /// Machine-readable content not to be displayed in GUI, but intended for system consumption or download to disk
        /// </summary>
        MachineReadable = 1
    }
}