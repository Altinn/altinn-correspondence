using System;
using System.Collections.Generic;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents status information about correspondence and digital letters associated with a service.
    /// </summary>
    public class CorrespondenceStatusDataResponseBE
    {
        /// <summary>
        /// Gets or sets the service code for the correspondences in the list.
        /// </summary>
        public string ServiceCode { get; set; }

        /// <summary>
        /// Gets or sets the service edition code for the correspondences in the list.
        /// </summary>
        public int ServiceEditionCode { get; set; }

        /// <summary>
        /// Gets or sets the list of correspondences and their statuses.
        /// </summary>
        public List<CorrespondenceStatusDetailsExternalBEV2> StatusList { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the result set is larger than the list can hold.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}
