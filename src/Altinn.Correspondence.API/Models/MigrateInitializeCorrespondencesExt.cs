using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

public class MigrateInitializeCorrespondencesExt
{
    /// <summary>
    /// The correspondence object that should be created
    /// </summary>
    [JsonPropertyName("correspondence")]
    public required BaseCorrespondenceExt Correspondence { get; set; }

    /// <summary>
    /// List of recipients for the correspondence, either as organization(urn:altinn:organization:identifier-no:ORGNR) or national identity number(urn:altinn:person:identifier-no:SSN)
    /// </summary>
    [JsonPropertyName("recipients")]
    [Required]
    public required List<string> Recipients { get; set; }

    /// <summary>
    /// Existing attachments that should be added to the correspondence
    /// </summary>
    [JsonPropertyName("existingAttachments")]
    public List<Guid> ExistingAttachments { get; set; } = new List<Guid>();

    /// <summary>
    /// Optional idempotency key to prevent duplicate correspondence creation
    /// </summary>
    [JsonPropertyName("idempotentKey")]
    public Guid? IdempotentKey { get; set; }
}
