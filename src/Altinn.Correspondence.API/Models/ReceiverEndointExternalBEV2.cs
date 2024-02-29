using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a receiver of a notification.
    /// </summary>
    public class ReceiverEndPointExternalBEV2
    {
        /// <summary>
        /// Gets or sets the internal Altinn ID of a receiver end point.
        /// </summary>
        /// <remarks>
        /// This is hidden in external interfaces.
        /// </remarks>
        public int ReceiverEndPointID { get; set; }

        /// <summary>
        /// Gets or sets what type of transport this receiver end point is. (Email, SMS.)
        /// </summary>
        public TransportTypeExternalV2? TransportType { get; set; }

        /// <summary>
        /// Gets or sets the address of the receiver. Email address or mobile phone number.
        /// </summary>
        public string ReceiverAddress { get; set; }

        /// <summary>
        /// Gets or sets the date and time for when the notification was sent to this receiver end point.
        /// </summary>
        /// <remarks>
        /// This is hidden in external interfaces.
        /// </remarks>
        public DateTime? SentDateTime { get; set; }

        /// <summary>
        /// Gets or sets the id of the parent notification.
        /// </summary>
        /// <remarks>
        /// This is hidden in external interfaces.
        /// </remarks>
        public int NotificationID { get; set; }
    }

    /// <summary>
    /// Represents a strongly typed list of ReceiverEndPoint elements that can be accessed by index.
    /// </summary>
    public class ReceiverEndPointExternalBEV2List : List<ReceiverEndPointExternalBEV2>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the query returned more rows than specified as the maximum number of rows.
        /// The user should narrow the search.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}