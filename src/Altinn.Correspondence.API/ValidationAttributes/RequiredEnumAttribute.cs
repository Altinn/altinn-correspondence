using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.API.ValidationAttributes;
public class RequiredEnumAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var fieldName = validationContext.DisplayName;

        if (value is null)
        {
            return new ValidationResult($"The {fieldName} field is required.");
        }

        if (!Enum.IsDefined(value.GetType(), value))
        {
            return new ValidationResult($"The {fieldName} field must be a defined value.");
        }

        return ValidationResult.Success;
    }
}