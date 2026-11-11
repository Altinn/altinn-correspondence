using System.Text.Json;
using System.Text.Json.Nodes;
using Altinn.Correspondence.Integrations.Dialogporten;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Altinn.Correspondence.Tests.Factories;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class DialogPatchRequestBuilderTests
{
    [Theory]
    [InlineData("""{"serviceResource":"urn:altinn:resource:attachment-resource","includeDialogParty":true,"unauthorizedPresentation":"Disabled"}""")]
    [InlineData("""{"serviceResource":"urn:altinn:resource:attachment-resource","dialogPartyChecks":[{"accessRestriction":"None"}],"unauthorizedPresentation":"Disabled"}""")]
    [InlineData("""{"dialogPartyChecks":[{"accessRestriction":"None"}],"unauthorizedPresentation":"Excluded"}""")]
    public void WithAddDownloadAllAttachmentsOperation_ShouldSendBackAuthorizationContext_WhenReAddingAttachmentsReadFromDialogporten(string dialogportenAuthorizationContextJson)
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithAttachment("first.pdf")
            .WithAttachment("second.pdf")
            .Build();
        var baseUrl = "https://example.com";
        var dialogportenAttachmentsJson = $$"""
            [{
                "id": "{{Guid.CreateVersion7()}}",
                "displayName": [{ "value": "first.pdf", "languageCode": "nb" }],
                "urls": [{ "url": "https://example.com/first.pdf", "consumerType": "Gui" }],
                "authorizationContext": {{dialogportenAuthorizationContextJson}}
            }]
            """;
        var existingDialogAttachments = JsonSerializer.Deserialize<List<Attachment>>(dialogportenAttachmentsJson, JsonSerializerOptions.Web)!;

        // Act
        var patchRequest = new DialogPatchRequestBuilder()
            .WithAddDownloadAllAttachmentsOperation(baseUrl, correspondence, existingDialogAttachments)
            .Build();

        // Assert
        var patchedAttachments = JsonSerializer.SerializeToNode(patchRequest)![0]!["value"]!.AsArray();
        Assert.Equal(2, patchedAttachments.Count);
        var patchedAuthorizationContext = patchedAttachments[1]!["authorizationContext"]!;
        Assert.All(JsonNode.Parse(dialogportenAuthorizationContextJson)!.AsObject(), property =>
            Assert.True(JsonNode.DeepEquals(property.Value, patchedAuthorizationContext[property.Key]), $"'{property.Key}' was not sent back unchanged"));
    }
}
