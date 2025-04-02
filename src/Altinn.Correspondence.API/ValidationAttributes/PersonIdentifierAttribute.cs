using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.API.Models;
public class PersonIdentifierAttribute : ValidationAttribute
{
    private static readonly string Pattern = $@"^(?:{UrnConstants.PersonIdAttribute}:)?\d{{11}}$";
    private static readonly Regex Regex = new(Pattern);
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string stringValue && IsValidSocialSecurityFormat(stringValue))
        {
            if (!stringValue.IsSocialSecurityNumber())
            {
                return new ValidationResult("The given Person national identity number is not valid");
            }
            else
            {
                return ValidationResult.Success;
            }

        }
        return new ValidationResult(ErrorMessage ?? "Invalid Person national identifier format or not valid number");
    }

    public static bool IsValidSocialSecurityFormat(string value)
    {
        return !string.IsNullOrEmpty(value) && Regex.IsMatch(value);
    }
}