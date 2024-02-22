#region Namespace imports

using System.Runtime.Serialization;

#endregion

namespace Altinn.Correspondence.Core.Models.Enums
{
    /// <summary>
    /// Enumeration types specifying the TransportTypes for sending the Notifications
    /// </summary>
    [DataContract(Name = "TransportType", Namespace = "http://schemas.altinn.no/serviceengine/formsengine/2009/10")]
    public enum TransportType
    {
        /// <summary>
        /// When the Transport Type is SMS
        /// </summary>
        [EnumMember]
        SMS = 1, 

        /// <summary>
        /// When the Transport Type is EMAIL
        /// </summary>
        [EnumMember]
        Email = 2, 

        /// <summary>
        /// When the Transport Type is IM
        /// </summary>
        [EnumMember]
        IM = 3, 

        /// <summary>
        /// When the Transport Type is BOTH(EMAIL AND SMS)
        /// </summary>
        [EnumMember]
        Both = 4,

        /// <summary>
        /// When the Transport Type is preferred to be SMS
        /// </summary>
        [EnumMember]
        SMSPreferred = 5,

        /// <summary>
        /// When the Transport Type is preferred to be EMAIL
        /// </summary>
        [EnumMember]
        EmailPreferred = 6,

        /// <summary>
        /// When first notification is preferred to be EMAIL, and followup notification is preferred to be SMS
        /// </summary>
        [EnumMember]
        PriorityEmailSMSReminder = 7
    }
}