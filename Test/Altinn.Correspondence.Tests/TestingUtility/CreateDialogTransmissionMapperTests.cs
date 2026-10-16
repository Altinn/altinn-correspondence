using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Tests.Factories;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class CreateDialogTransmissionMapperTests
{
    [Fact]
    public void CreateDialogTransmission_ShouldRestrictAttachmentToItsResource_WhenAttachmentResourceDiffersFromCorrespondenceResource()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithResourceId("correspondence-resource")
            .WithAttachmentForResource("restricted.pdf", "attachment-resource")
            .Build();
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogTransmissionMapper.CreateDialogTransmission(correspondence, baseUrl);

        // Assert
        var authorizationContext = Assert.Single(result.Attachments).AuthorizationContext;
        Assert.NotNull(authorizationContext);
        Assert.Equal("urn:altinn:resource:attachment-resource", authorizationContext.ServiceResource);
    }
}
