using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Constants;

namespace Altinn.Correspondence.API.Models;
public class OrganizationNumberAttribute : ValidationAttribute
{
    private static readonly string Pattern = $@"^(?:0192:|{UrnConstants.OrganizationNumberAttribute}:)\d{{9}}$";
    private static readonly Regex Regex = new(Pattern);
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string stringValue && IsValidOrganizationNumber(stringValue))
        {
            return ValidationResult.Success;
        }
        return new ValidationResult(ErrorMessage ?? "Invalid organization number format");
    }
    public static bool IsValidOrganizationNumber(string value)
    {
        return !string.IsNullOrEmpty(value) && Regex.IsMatch(value);
    }
}