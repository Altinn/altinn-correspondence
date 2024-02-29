namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Enumeration for current status for a SDP message
    /// </summary>
    public enum SdpStatusTypeExternal
    {
        /// <summary>
        /// Unknown status. Either not set, or we were unable to retrieve it.
        /// Also used if there is no new status when trying to retrieve status.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Message is sent to message conveyor
        /// </summary>
        Sent_Meldingsformidler = 1,

        /// <summary>
        /// Message is delivered to end user
        /// </summary>
        Delivered_EndUser = 2,

        /// <summary>
        /// Message has failed to be delivered to end user
        /// </summary>
        Delivery_EndUser_Failed = 3,

        /// <summary>
        /// The reportee has opted not to allow electronic communication.
        /// </summary>
        Reserved = 4,

        /// <summary>
        /// The reportee has not created any electronic mail box.
        /// </summary>
        NoMailBox = 5
    }
}
