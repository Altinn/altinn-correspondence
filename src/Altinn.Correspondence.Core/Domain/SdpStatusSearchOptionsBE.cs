using System;

namespace Altinn.Correspondence.Core.Models
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

        /// <summary>
        /// Create a new instance of the SdpStatusSearchOptionsBE class with data from a SdpStatusSearchOptionsExternalBE object.
        /// </summary>
        /// <param name="external">The SdpStatusSearchOptionsExternalBE object to get initialization data from.</param>
        /// <returns>A new, populated SdpStatusSearchOptionsBE object.</returns>
        public static SdpStatusSearchOptionsBE Create(SdpStatusSearchOptionsExternalBE external)
        {
            if (external == null)
            {
                return null;
            }

            return new SdpStatusSearchOptionsBE
            {
                IncludeCorrespondence = external.IncludeCorrespondence
            };
        }
    }
}
