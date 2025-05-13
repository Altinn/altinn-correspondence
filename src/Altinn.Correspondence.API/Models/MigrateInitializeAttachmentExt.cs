using Altinn.Correspondence.Common.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// Represents a container object for attachments used when initiating a shared attachment
/// </summary>
public class MigrateInitializeAttachmentExt : BaseAttachmentExt
{
    /// <summary>
    /// Gets or sets the Resource Id for the correspondence service.
    /// </summary>
    [JsonPropertyName("resourceId")]
    [StringLength(255, MinimumLength = 1)]
    [Required]
    public required string ResourceId { get; set; }

    /// <summary>
    /// The Sending organisation of the correspondence. 
    /// </summary>
    /// <remarks>
    /// Organization number in countrycode:organizationnumber format.
    /// </remarks>
    [JsonPropertyName("sender")]
    [OrganizationNumber(ErrorMessage = $"Organization numbers should be on the format '{UrnConstants.OrganizationNumberAttribute}:organizationnumber' or the format countrycode:organizationnumber, for instance 0192:910753614")]
    [Required]
    public required string Sender { get; set; }

    [JsonPropertyName("altinn2AttachmentId")]
    public int? Altinn2AttachmentId { get; set; }
}
