namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines intended consumer for attachment
    /// </summary>
    public enum ConsumerTypeExt : int
    {
        /// <summary>
        /// Human-readable content to be displayed in GUI
        /// </summary>
        Gui = 0, 

        /// <summary>
        /// Machine-readable content not to be displayed in GUI
        /// </summary>
        Api = 1,
    }
}