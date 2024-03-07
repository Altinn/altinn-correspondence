using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a reference to another item in the Altinn ecosystem    
    /// </summary>
    public class ExternalReferenceExt
    {
        public required string ReferenceValue { get; set; }
        public required ReferenceTypeExt ReferenceType { get; set; }
    }
}