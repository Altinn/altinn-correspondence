namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines where an attachment should be available.
    /// </summary>
    public enum ReferenceTypeExt : int
    {
        /// <summary>
        /// Specifies a generic reference
        /// </summary>
        Generic = 0, 

        /// <summary>
        /// Specifies that the reference is to a Altinn 2 Form Task / Archive Reference
        /// </summary>
        Altinn2ArchiveReference = 1,

        /// <summary>
        /// Specifies that the reference is to a Altinn 2 Collaboration Service
        /// </summary>
        Altinn2CaseId = 2,

        /// <summary>
        /// Specifies that the reference is to a Altinn App Instance
        /// </summary>
        AltinnAppInstance = 3,

        /// <summary>
        /// Specifies that the reference is to a Altinn Broker File Transfer
        /// </summary>
        AltinnBrokerFileTransfer = 4
    }
}