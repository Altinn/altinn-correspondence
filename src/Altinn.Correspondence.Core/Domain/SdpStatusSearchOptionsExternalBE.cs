using System;
using System.Runtime.Serialization;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents the search options that can be set when performing a search for status details on digital letters.
    /// </summary>
    [DataContract(Name = "SdpStatusSearchOptions", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2016/02")]
    public class SdpStatusSearchOptionsExternalBE
    {
        /// <summary>
        /// Gets or sets a value indicating whether the search should include correspondence or if the logic should save time 
        /// and only search for secure digital post elements.
        /// </summary>
        [DataMember(IsRequired = true)]
        public bool IncludeCorrespondence { get; set; }
    }
}
