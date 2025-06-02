using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Correspondence.Application;

namespace Altinn.Correspondence.API.Models;

public class InitializeCorrespondencesExt
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
    [RecipientList]
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
    [ValidIdempotentKey]
    public Guid? IdempotentKey { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
internal class ValidIdempotentKeyAttribute : ValidationAttribute
{
    public ValidIdempotentKeyAttribute()
    {
        ErrorMessage = CorrespondenceErrors.InvalidIdempotencyKey.Message;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (value is not Guid guid)
        {
            return new ValidationResult(CorrespondenceErrors.InvalidIdempotencyKey.Message);
        }

        if (guid == Guid.Empty)
        {
            return new ValidationResult(CorrespondenceErrors.InvalidIdempotencyKey.Message);
        }

        return ValidationResult.Success;
    }
}
