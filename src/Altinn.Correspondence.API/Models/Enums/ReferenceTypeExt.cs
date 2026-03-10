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
        /// Specifies that the reference is a Dialogporten Dialog ID
        /// </summary>
        DialogportenDialogId = 3,

        /// <summary>
        /// Specifies that the reference is a Dialogporten Process ID
        /// </summary>
        DialogportenProcessId = 4,
        /// <summary>
        /// Specifies that the reference is a Dialogporten Transmission ID
        /// </summary>
        DialogportenTransmissionId = 5,

        /// <summary>
        /// Specifies that the reference is a Dialogporten Transmission Type
        /// </summary>
        DialogportenTransmissionType = 6,

        /// <summary>
        /// Specifies that the reference is a Dialogporten Dialog Status
        /// </summary>
        DialogportenDialogStatus = 7,

        /// <summary>
        /// Specifies that the reference is a Dialogporten Dialog Extended Status.
        /// The corresponding referenceValue must be 25 characters or fewer.
        /// </summary>
        DialogportenDialogExtendedStatus = 8,
    }
}