using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a secure digital post element with key values and status history.
    /// </summary>
    [DataContract(Name = "SdpStatusDetails", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2016/02")]
    public class SdpStatusDetailsExternalBE
    {
        /// <summary>
        /// Gets or sets the unique id of a secure digital post element as it is stored in Altinn.
        /// </summary>
        [DataMember]
        public int SdpId { get; set; }

        /// <summary>
        /// Gets or sets unique id of the correspondence that was created at the same time as the digital letter. This is null
        /// if no correspondence was created.
        /// </summary>
        [DataMember]
        public int? CorrespondenceId { get; set; }

        /// <summary>
        /// Gets or sets the date for when the digital letter was created.
        /// </summary>
        [DataMember]
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Gets or sets the date and time for when the digital letter element was updated in Altinn. This is updated with every status change.
        /// </summary>
        [DataMember]
        public DateTime LastChangedDateTime { get; set; }

        /// <summary>
        /// Gets or sets a identifying value for the reportee. This can be a social security number or organization number.
        /// </summary>
        [DataMember]
        public string Reportee { get; set; }

        /// <summary>
        /// Gets or sets the reference value that was provided by the agency that created the secure digital post element. 
        /// ExternalShipmentReference or SendersReference.
        /// </summary>
        [DataMember]
        public string Reference { get; set; }

        /// <summary>
        /// Gets or sets the status history for the secure digital post element.
        /// </summary>
        [DataMember]
        public List<SdpStatusChangeExternalBE> StatusHistory { get; set; }
    }
}
