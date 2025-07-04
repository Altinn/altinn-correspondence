using System.ComponentModel.DataAnnotations;
using Altinn.Correspondence.API.ValidationAttributes;

namespace Altinn.Correspondence.Tests.TestingUtility
{
    public class OnBehalfOfAttributeTests
    {
        private readonly OnBehalfOfAttribute _attribute;
        private readonly ValidationContext _validationContext;

        public OnBehalfOfAttributeTests()
        {
            _attribute = new OnBehalfOfAttribute();
            _validationContext = new ValidationContext(new object())
            {
                DisplayName = "OnBehalfOf"
            };
        }

        [Fact]
        public void IsValid_ReturnsSuccess_WhenValueIsNull()
        {
            // Act
            var result = _attribute.GetValidationResult(null, _validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }


        [Fact]
        public void IsValid_ReturnsError_WhenValueIsNotString()
        {
            // Act
            var result = _attribute.GetValidationResult(123, _validationContext);

            // Assert
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("OnBehalfOf must be a string", result?.ErrorMessage);
        }

        [Theory]
        [InlineData("urn:altinn:organization:identifier-no:123456789")]
        [InlineData("0192:123456789")]
        public void IsValid_ReturnsSuccess_WhenValueIsValidOrganizationNumber(string orgNumber)
        {
            // Act
            var result = _attribute.GetValidationResult(orgNumber, _validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("urn:altinn:person:identifier-no:13876698239")]
        [InlineData("13876698239")]
        public void IsValid_ReturnsSuccess_WhenValueIsValidSocialSecurityNumber(string ssn)
        {
            // Act
            var result = _attribute.GetValidationResult(ssn, _validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("     ")]
        [InlineData("invalid")]
        [InlineData("12345")]
        [InlineData("urn:altinn:invalid:123456789")]
        [InlineData("0192:12345678")]
        [InlineData("0192:1234567890")]
        [InlineData("urn:altinn:organization:identifier-no:12345678")]
        [InlineData("urn:altinn:organization:identifier-no:1234567890")]
        [InlineData("urn:altinn:person:identifier-no:1234567890")]
        [InlineData("urn:altinn:person:identifier-no:123456789012")]
        public void IsValid_ReturnsError_WhenValueIsInvalidFormat(string invalidValue)
        {
            // Act
            var result = _attribute.GetValidationResult(invalidValue, _validationContext);

            // Assert
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("OnBehalfOf must be either a valid organization number", result?.ErrorMessage);
        }
    }
} 