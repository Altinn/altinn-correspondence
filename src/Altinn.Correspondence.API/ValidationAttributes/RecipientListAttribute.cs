using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.API.Models;

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
            var emailUrnRegex = new Regex($@"^{Regex.Escape(UrnConstants.PersonIdPortenEmailAttribute)}:.+$");
            var legacySelfIdentifiedUrnRegex = new Regex($@"^{Regex.Escape(UrnConstants.PersonLegacySelfIdentifiedAttribute)}:.+$");
            
            if (!orgRegex.IsMatch(recipient) && !personRegex.IsMatch(recipient) && !emailUrnRegex.IsMatch(recipient) && !legacySelfIdentifiedUrnRegex.IsMatch(recipient))
            {
                return new ValidationResult($"Recipient should be an organization number in the format '{UrnConstants.OrganizationNumberAttribute}:organizationnumber' or the format countrycode:organizationnumber, for instance 0192:910753614, a national identity number, an idporten email URN in the format '{UrnConstants.PersonIdPortenEmailAttribute}:email', or a legacy selfidentified URN in the format '{UrnConstants.PersonLegacySelfIdentifiedAttribute}:username'");
            }
            if (personRegex.IsMatch(recipient) && !recipient.IsSocialSecurityNumber())
            {
                return new ValidationResult("The given Recipient national identity number is not valid");
            }
            if (emailUrnRegex.IsMatch(recipient) && !recipient.IsEmailAddress())
            {
                return new ValidationResult("The given Recipient email address is not valid");
            }
        }

        return ValidationResult.Success;
    }
}
