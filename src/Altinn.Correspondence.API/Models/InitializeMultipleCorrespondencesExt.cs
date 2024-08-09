using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

public class InitializeMultipleCorrespondencesExt
{
    /// <summary>
    /// The correspondence object that should be created
    /// </summary>
    [JsonPropertyName("correspondence")]
    public required BaseCorrespondenceObject Correspondence { get; set; }

    /// <summary>
    /// The recipients of the correspondence, either an organisation or an person
    /// </summary>
    [JsonPropertyName("recipients")]
    [Required]
    [MinLength(1, ErrorMessage = "At least one recipient is required")]
    public required List<string> Recipients { get; set; }
}
