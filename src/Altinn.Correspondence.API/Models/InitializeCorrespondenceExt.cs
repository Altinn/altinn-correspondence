using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a request object for the operation, InitializeCorrespondence, that can create a correspondence in Altinn.    
    /// </summary>
    public class InitializeCorrespondenceExt : BaseCorrespondenceObject
    {

        /// <summary>
        /// The recipient of the correspondence, either an organisation or an person
        /// </summary>
        /// <remarks>
        /// National identity number or Organization number.
        /// </remarks
        [JsonPropertyName("recipient")]
        [RegularExpression(@"^\d{4}:\d{9}$|\d{11}$", ErrorMessage = "Recipient should be an organization number in the form countrycode:organizationnumber, for instance 0192:910753614 or a national identity number")]
        [Required]
        public required string Recipient { get; set; }

    }
}