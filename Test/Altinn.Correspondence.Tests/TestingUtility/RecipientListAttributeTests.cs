using System.ComponentModel.DataAnnotations;
using Altinn.Correspondence.API.Models;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class RecipientListAttributeTests
{
    private readonly RecipientListAttribute _attribute = new();
    private readonly ValidationContext _validationContext = new(new object())
    {
        DisplayName = "Recipients"
    };

    [Fact]
    public void IsValid_ReturnsSuccess_For_Valid_IdPorten_Email_Urn()
    {
        var result = _attribute.GetValidationResult(
            new List<string> { "urn:altinn:person:idporten-email:test@example.com" },
            _validationContext);

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_ReturnsError_For_Legacy_IdPorten_Email_SubPrefix()
    {
        var result = _attribute.GetValidationResult(
            new List<string> { "urn:altinn:person:idporten-email:epost:test@example.com" },
            _validationContext);

        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Equal("The given Recipient email address is not valid", result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_ReturnsSuccess_For_Valid_Legacy_SelfIdentified_Urn()
    {
        var result = _attribute.GetValidationResult(
            new List<string> { "urn:altinn:person:legacy-selfidentified:axely123" },
            _validationContext);

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_ReturnsError_For_Legacy_SelfIdentified_Urn_With_Colon_In_Username()
    {
        var result = _attribute.GetValidationResult(
            new List<string> { "urn:altinn:person:legacy-selfidentified:email:test@example.com" },
            _validationContext);

        Assert.NotEqual(ValidationResult.Success, result);
    }
}
