#region Namespace imports

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Altinn.Correspondence.Core.Domain.Models.Enums;

#endregion

namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// Internal receiver end point details
    /// </summary>
    [Serializable]
    [DataContract(Name = "InternalReceiverEndPoint", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Notification/2009/10")]
    public class InternalReceiverEndPointBE
    {
        /// <summary>
        /// Gets or sets Identifier for a Receiver End Point
        /// </summary>
        public int ReceiverEndPointID { get; set; }

        /// <summary>
        /// Gets or sets Identifier for a Receiver Address
        /// </summary>
        [DataMember]
        public string ReceiverAddress { get; set; }

        /// <summary>
        /// Gets or sets Identifier for Sent DateTime
        /// </summary>
        [DataMember]
        public DateTime? SentDateTime { get; set; }

        /// <summary>
        /// Gets or sets Identifier for Notification
        /// </summary>
        public int NotificationID { get; set; }

        /// <summary>
        /// Gets or sets Transport Id
        /// </summary>
        [DataMember]
        public TransportType? TransportId { get; set; }
    }

    /// <summary>Internal receiver end point list</summary>
    [Serializable]
    [CollectionDataContract(Namespace = "http://schemas.altinn.no/services/ServiceEngine/Notification/2009/10")]
    public class InternalReceiverEndPointBEList : List<InternalReceiverEndPointBE>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the query returned more rows than specified as the maximum number of rows.
        /// </summary>
        [DataMember]
        public bool LimitReached { get; set; }
    }
}