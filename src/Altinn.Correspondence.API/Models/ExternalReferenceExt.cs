using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a reference to another item in the Altinn ecosystem    
    /// </summary>
    public class ExternalReferenceExt
    {
        /// <summary>
        /// The Reference Value
        /// </summary>
        [JsonPropertyName("referenceValue")]
        public required string ReferenceValue { get; set; }

        /// <summary>
        /// The Type of reference
        /// </summary>
        [JsonPropertyName("referenceType")]
        public required ReferenceTypeExt ReferenceType { get; set; }
    }
}