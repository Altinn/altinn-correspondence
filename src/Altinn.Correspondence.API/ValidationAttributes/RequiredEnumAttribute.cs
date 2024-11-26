using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.API.Models;
public class RequiredEnumAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value == null || !Enum.IsDefined(value.GetType(), value))
        {
            var fieldName = validationContext.DisplayName;
            return new ValidationResult($"The {fieldName} field is required.");
        }
        return ValidationResult.Success;
    }
}