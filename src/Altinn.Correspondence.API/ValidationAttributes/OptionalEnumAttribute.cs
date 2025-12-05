using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.API.ValidationAttributes;

public class OptionalEnumAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (!Enum.IsDefined(value.GetType(), value))
        {
            var fieldName = validationContext.DisplayName;
            return new ValidationResult($"The {fieldName} field must be a defined value.");
        }

        return ValidationResult.Success;
    }
}