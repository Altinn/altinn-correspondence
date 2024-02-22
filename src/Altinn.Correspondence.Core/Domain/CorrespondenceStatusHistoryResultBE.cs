using System;
namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents the response from the CorrespondenceStatusHistory operation in the CorrespondenceAgency service.
    /// </summary>
    public class CorrespondenceStatusHistoryResultBE
    {
        /// <summary>
        /// Gets or sets the list of correspondences and their statuses.
        /// </summary>
        public CorrespondenceStatusInformationExternalBE CorrespondenceStatusInformation { get; set; }

        /// <summary>
        /// Gets or sets the list of Sdp entities and their statuses.
        /// </summary>
        public SdpStatusInformationExternalBE SdpStatusInformation { get; set; }
    }
}