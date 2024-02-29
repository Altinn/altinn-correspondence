using System;
using System.Collections.Generic;

namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// Represents a set of data with details and status information about secure digital post elements.
    /// </summary>
    public class SdpStatusInformationBE
    {
        /// <summary>
        /// Gets or sets a list of secure digital post elements with details and status information.
        /// </summary>
        public List<SdpStatusDetailsBE> SdpStatusDetailsList { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there exists more elements than currently shown in the list.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}
