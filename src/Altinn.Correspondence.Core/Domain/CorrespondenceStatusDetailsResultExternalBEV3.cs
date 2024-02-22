using System;
using System.Runtime.Serialization;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents the response from the GetCorrespondenceStatusDetailsV3 operation in the CorrespondenceAgency service.
    /// </summary>
    [DataContract(Name = "CorrespondenceStatusResultV3", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2016/02")]
    public class CorrespondenceStatusDetailsResultExternalBEV3
    {
        /// <summary>
        /// Gets or sets the service code for the correspondences in the list.
        /// </summary>
        [DataMember]
        public string ServiceCode { get; set; }

        /// <summary>
        /// Gets or sets the service edition code for the correspondences in the list.
        /// </summary>CorrespondenceStatusInformationExternalBE
        [DataMember]
        public int ServiceEditionCode { get; set; }

        /// <summary>
        /// Gets or sets the list of correspondences and their statuses.
        /// </summary>
        [DataMember]
        public CorrespondenceStatusInformationExternalBE CorrespondenceStatusInformation { get; set; }

        /// <summary>
        /// Gets or sets the list of correspondences and their statuses.
        /// </summary>
        [DataMember]
        public SdpStatusInformationExternalBE SdpStatusInformation { get; set; }
    }
}