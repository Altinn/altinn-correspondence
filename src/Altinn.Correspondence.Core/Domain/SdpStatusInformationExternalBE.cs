using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents a set of data with details and status information about secure digital post elements.
    /// </summary>
    [DataContract(Name = "SdpStatusInformation", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2016/02")]
    public class SdpStatusInformationExternalBE
    {
        /// <summary>
        /// Gets or sets the list of correspondences and their statuses.
        /// </summary>
        [DataMember]
        public List<SdpStatusDetailsExternalBE> SdpStatusDetailsList { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the result set is larger than the list can hold.
        /// </summary>
        [DataMember]
        public bool LimitReached { get; set; }

        /// <summary>
        /// Create a new instance of the SdpStatusInformationExternalBE class with data from a SdpStatusInformationBE object.
        /// </summary>
        /// <param name="internalSdpInfo">The SdpStatusInformationBE object to get initialization data from.</param>
        /// <returns>A new, populated SdpStatusInformationExternalBE object.</returns>
        public static SdpStatusInformationExternalBE Create(SdpStatusInformationBE internalSdpInfo)
        {
            if (internalSdpInfo == null)
            {
                return null;
            }

            SdpStatusInformationExternalBE externalSdpInfo = new SdpStatusInformationExternalBE
            {
                LimitReached = internalSdpInfo.LimitReached,
                SdpStatusDetailsList = internalSdpInfo.SdpStatusDetailsList.Select(SdpStatusDetailsExternalBE.Create).ToList()
            };

            return externalSdpInfo;
        }
    }
}
