using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Constants;

namespace Altinn.Correspondence.API.Models;

public class InitializeCorrespondencesExt
{
    /// <summary>
    /// The correspondence object that should be created
    /// </summary>
    [JsonPropertyName("correspondence")]
    public required BaseCorrespondenceExt Correspondence { get; set; }

    /// <summary>
    /// The recipients of the correspondence, either an organisation or an person
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


    [AttributeUsage(AttributeTargets.Property)]
    internal class RecipientListAttribute : ValidationAttribute
    {
        public RecipientListAttribute()
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            if (!(value is List<string>))
            {
                return new ValidationResult("Recipients Object is not of proper type");
            }

            var recipients = (List<string>)value;
            if (recipients.Count == 0)
                return new ValidationResult("Recipients can not be empty");

            if (recipients.Count > 500)
                return new ValidationResult("Recipients can contain at most 500 recipients");

            foreach (var recipient in recipients)
            {
                var orgRegex = new Regex($@"^(?:0192:|{UrnConstants.OrganizationNumberAttribute}:)\d{{9}}$");
                var personRegex = new Regex($@"^(?:{UrnConstants.PersonIdAttribute}:)?\d{{11}}$");
                if (!orgRegex.IsMatch(recipient) && !personRegex.IsMatch(recipient))
                {
                    return new ValidationResult($"Recipient should be an organization number in the format '{UrnConstants.OrganizationNumberAttribute}:organizationnumber' or the format countrycode:organizationnumber, for instance 0192:910753614, or a national identity number");
                }
            }

            return ValidationResult.Success;
        }
    }
}
