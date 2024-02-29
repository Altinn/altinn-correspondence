using System;

namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// Represents the search options that can be set when performing a search for status details on digital letters.
    /// </summary>
    public class SdpStatusSearchOptionsBE
    {
        /// <summary>
        /// Gets or sets a value indicating whether the search should include correspondence or if the logic should save time 
        /// and only search for secure digital post elements.
        /// </summary>
        public bool IncludeCorrespondence { get; set; }
    }
}
