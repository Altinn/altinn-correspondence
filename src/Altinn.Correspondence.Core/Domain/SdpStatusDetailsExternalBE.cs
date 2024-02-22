using System;
using System.Collections.Generic;
using System.Linq;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents a secure digital post element with key values and status history.
    /// </summary>
    public class SdpStatusDetailsExternalBE
    {
        /// <summary>
        /// Gets or sets the unique id of a secure digital post element as it is stored in Altinn.
        /// </summary>
        public int SdpId { get; set; }

        /// <summary>
        /// Gets or sets unique id of the correspondence that was created at the same time as the digital letter. This is null
        /// if no correspondence was created.
        /// </summary>
        public int? CorrespondenceId { get; set; }

        /// <summary>
        /// Gets or sets the date for when the digital letter was created.
        /// </summary>
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Gets or sets the date and time for when the digital letter element was updated in Altinn. This is updated with every status change.
        /// </summary>
        public DateTime LastChangedDateTime { get; set; }

        /// <summary>
        /// Gets or sets a identifying value for the reportee. This can be a social security number or organization number.
        /// </summary>
        public string Reportee { get; set; }

        /// <summary>
        /// Gets or sets the reference value that was provided by the agency that created the secure digital post element. 
        /// ExternalShipmentReference or SendersReference.
        /// </summary>
        public string Reference { get; set; }

        /// <summary>
        /// Gets or sets the status history for the secure digital post element.
        /// </summary>
        public List<SdpStatusChangeExternalBE> StatusHistory { get; set; }

        /// <summary>
        /// Create a new instance of the SdpStatusDetailsExternalBE class with data from a SdpStatusDetailsBE object.
        /// </summary>
        /// <param name="internalSdpStatus">The SdpStatusDetailsBE object to get initialization data from.</param>
        /// <returns>A new, populated SdpStatusDetailsExternalBE object.</returns>
        public static SdpStatusDetailsExternalBE Create(SdpStatusDetailsBE internalSdpStatus)
        {
            SdpStatusDetailsExternalBE externalSdpStatus = new SdpStatusDetailsExternalBE
            {
                SdpId = internalSdpStatus.SdpId,
                CorrespondenceId = internalSdpStatus.CorrespondenceId,
                CreatedDateTime = internalSdpStatus.CreatedDateTime,
                LastChangedDateTime = internalSdpStatus.LastChangedDateTime,
                Reportee = internalSdpStatus.Reportee,
                Reference = internalSdpStatus.Reference,
                StatusHistory = internalSdpStatus.StatusHistory.Select(SdpStatusChangeExternalBE.Create).ToList()
            };

            return externalSdpStatus;
        }
    }
}
