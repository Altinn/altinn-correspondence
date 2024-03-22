namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines intended presentation for attachment
    /// </summary>
    public enum IntendedPresentationTypeExt : int
    {
        /// <summary>
        /// Main Human-readable Message Body to be displayed in GUI
        /// </summary>
        MessageBody = 0,

        /// <summary>
        /// Human-readable content to be displayed in GUI
        /// </summary>
        HumanReadable = 1, 

        /// <summary>
        /// Machine-readable content not to be displayed in GUI, but intended for sy
        /// </summary>
        MachineReadable = 2
    }
}