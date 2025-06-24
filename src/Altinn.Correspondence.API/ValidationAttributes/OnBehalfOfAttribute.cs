using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.API.ValidationAttributes;
public class OnBehalfOfAttribute : ValidationAttribute
{
    private static readonly string OrgPattern = $@"^(?:0192:|{UrnConstants.OrganizationNumberAttribute}:)\d{{9}}$";
    private static readonly string SsnPattern = $@"^(?:{UrnConstants.PersonIdAttribute}:)?\d{{11}}$";
    private static readonly Regex OrgRegex = new(OrgPattern);
    private static readonly Regex SsnRegex = new(SsnPattern);

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (value is not string stringValue)
        {
            return new ValidationResult("OnBehalfOf must be a string");
        }

        if (OrgRegex.IsMatch(stringValue))
        {
            return ValidationResult.Success;
        }

        if (SsnRegex.IsMatch(stringValue))
        {
            if (!stringValue.IsSocialSecurityNumber())
            {
                return new ValidationResult("The given OnBehalfOf national identity number is not valid");
            }
            return ValidationResult.Success;
        }

        return new ValidationResult(ErrorMessage ?? 
            $"OnBehalfOf must be either a valid organization number (format: '{UrnConstants.OrganizationNumberAttribute}:organizationnumber' or '0192:organizationnumber') or a valid national identity number (format: '{UrnConstants.PersonIdAttribute}:nationalidentitynumber' or just 'nationalidentitynumber')");
    }
}