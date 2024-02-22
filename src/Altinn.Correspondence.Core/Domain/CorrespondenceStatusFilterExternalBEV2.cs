using System;
using System.Runtime.Serialization;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents the request object used as input to the GetCorrespondenceStatusDetailsV2 operation in the CorrespondenceAgency service.
    /// It has fields for different filter options presented by the operation.
    /// </summary>
    [DataContract(Name = "CorrespondenceStatusFilterV2", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2014/10")]
    public class CorrespondenceStatusFilterExternalBEV2
    {
        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the service code. This field is mandatory.
        /// </summary>
        [DataMember(IsRequired = true)]
        public string ServiceCode { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the service edition code. This field is mandatory.
        /// </summary>
        [DataMember(IsRequired = true)]
        public int ServiceEditionCode { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the senders reference value on the correspondence.
        /// </summary>
        [DataMember]
        public string SendersReference { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the recipient of the correspondence.
        /// </summary>
        /// <remarks>
        /// Value must be an organization number or social security number.
        /// </remarks>
        [DataMember]
        public string Reportee { get; set; }

        /// <summary>
        /// Gets or sets the party id of the given Reportee. 
        /// </summary>
        /// <remarks>
        /// The field is used internally which is why it doesn't have the "DataMember" attribute.
        /// </remarks>
        public int PartyId { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the creation date of the correspondence.
        /// Includes correspondence newer than the set date.
        /// </summary>
        [DataMember]
        public DateTime? CreatedAfterDate { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the creation date of the correspondence.
        /// Includes correspondence older than the set date.
        /// </summary>
        [DataMember]
        public DateTime? CreatedBeforeDate { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the current status of the correspondence.
        /// </summary>
        [DataMember]
        public CorrespondenceStatusTypeAgencyExternalV2 CurrentStatus { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the status of notifications.
        /// </summary>
        [DataMember]
        public bool? NotificationSent { get; set; }
    }
}
