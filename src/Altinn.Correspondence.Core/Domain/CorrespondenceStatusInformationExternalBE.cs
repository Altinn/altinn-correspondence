using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents a set of data with details and status information about correspondence elements.
    /// </summary>
    [DataContract(Name = "CorrespondenceStatusInformation", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2016/02")]
    public class CorrespondenceStatusInformationExternalBE
    {
        /// <summary>
        /// Gets or sets the list of correspondences and their statuses.
        /// </summary>
        [DataMember]
        public List<CorrespondenceStatusDetailsExternalBEV2> CorrespondenceStatusDetailsList { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the result set is larger than the list can hold.
        /// </summary>
        [DataMember]
        public bool LimitReached { get; set; }
    }
}
