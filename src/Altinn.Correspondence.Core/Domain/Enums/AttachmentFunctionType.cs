#region Namespace imports

using System.Runtime.Serialization;

#endregion

namespace Altinn.Correspondence.Core.Domain.Models.Enums
{
    /// <summary>
    ///  Enumeration types for Attachment function.
    /// </summary>
    [DataContract(Name = "AttachmentFunctionType", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Subscription/2009/10")]
    public enum AttachmentFunctionType : int
    {
        /// <summary>
        /// Default Value
        /// </summary>
        [EnumMember]
        Default = 0, 

        /// <summary>
        /// When the function of the attachment is not specified.
        /// </summary>
        [EnumMember]
        Unspecified = 1, 

        /// <summary>
        /// When the function of attachment is for Legacy system.
        /// </summary>
        [EnumMember]
        Invoice = 2, 
    }
}