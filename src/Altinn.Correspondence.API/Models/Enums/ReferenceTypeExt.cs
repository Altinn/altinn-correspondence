namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines what kind of reference
    /// </summary>
    public enum ReferenceTypeExt : int
    {
        /// <summary>
        /// Specifies a generic reference
        /// </summary>
        Generic = 0,

        /// <summary>
        /// Specifies that the reference is to a Altinn App Instance
        /// </summary>
        AltinnAppInstance = 1,

        /// <summary>
        /// Specifies that the reference is to a Altinn Broker File Transfer
        /// </summary>
        AltinnBrokerFileTransfer = 2,

        /// <summary>
        /// Specifies that the reference is a DialogPorten Dialog ID
        /// </summary>
        DialogPortenDialogID = 3,

        /// <summary>
        /// Specifies that the reference is a DialogPorten Dialog Element ID
        /// </summary>
        DialogPortenDialogElementID = 4,
    }
}