#region Namespace imports

using System.Runtime.Serialization;

#endregion

namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Enumeration types specifying the TransportTypes for sending the Notifications
    /// </summary>
    public enum TransportTypeExternalV2
    {
        /// <summary>
        /// When the Transport Type is SMS
        /// </summary>
        SMS = 1,

        /// <summary>
        /// When the Transport Type is EMAIL
        /// </summary>
        Email = 2,

        /// <summary>
        /// When the Transport Type is IM
        /// </summary>
        IM = 3,

        /// <summary>
        /// When the Transport Type is BOTH(EMAIL AND SMS)
        /// </summary>
        Both = 4,

        /// <summary>
        /// When the Transport Type is preferred to be SMS
        /// </summary>
        SMSPreferred = 5,

        /// <summary>
        /// When the Transport Type is preferred to be EMAIL
        /// </summary>
        EmailPreferred = 6,

        /// <summary>
        /// When the Transport Type is preferred to be EMAIL
        /// </summary>
        PriorityEmailSMSReminder = 7
    }
}