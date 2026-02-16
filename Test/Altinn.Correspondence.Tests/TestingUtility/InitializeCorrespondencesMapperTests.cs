using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Mappers;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class InitializeCorrespondencesMapperTests
{
    private static InitializeCorrespondencesExt CreateMinimalRequest(List<string> recipients)
    {
        return new InitializeCorrespondencesExt
        {
            Correspondence = new BaseCorrespondenceExt
            {
                ResourceId = "unit-test-resource",
                SendersReference = "test-ref",
                Content = new InitializeCorrespondenceContentExt
                {
                    Language = "nb",
                    MessageTitle = "Test",
                    MessageSummary = "",
                    MessageBody = ""
                }
            },
            Recipients = recipients
        };
    }

    [Theory]
    [InlineData("0192:123456789", "urn:altinn:organization:identifier-no:123456789")]
    [InlineData("urn:altinn:organization:identifier-no:123456789", "urn:altinn:organization:identifier-no:123456789")]
    [InlineData("123456789", "urn:altinn:organization:identifier-no:123456789")]
    [InlineData("urn:altinn:person:idporten-email:User@Example.com", "urn:altinn:person:idporten-email:user@example.com")]
    [InlineData("07827199405", "urn:altinn:person:identifier-no:07827199405")]
    [InlineData("urn:altinn:person:identifier-no:07827199405", "urn:altinn:person:identifier-no:07827199405")]
    public void MapToRequest_Normalizes_Recipient_To_Urn(string recipientInput, string expectedMappedRecipient)
    {
        var request = CreateMinimalRequest([recipientInput]);
        var result = InitializeCorrespondencesMapper.MapToRequest(request);

        Assert.True(result.IsT0);
        var initRequest = result.AsT0;
        Assert.Single(initRequest.Recipients);
        Assert.Equal(expectedMappedRecipient, initRequest.Recipients[0]);
    }
}
