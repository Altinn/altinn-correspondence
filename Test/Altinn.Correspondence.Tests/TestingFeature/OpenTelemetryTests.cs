using Altinn.Correspondence.Integrations.OpenTelemetry;

namespace Altinn.Correspondence.Tests.OpenTelemetry;

public class OpenTelemetryTests
{
    [Theory]
    [InlineData(
    "/dialogporten/api/v1/serviceowner/dialogs/00000000-0000-0000-0000-000000000000/activities",
    "/dialogporten/api/v1/serviceowner/dialogs/{id}/activities")]
    [InlineData(
    "/resourceregistry/api/v1/resource/correspondence-test-resource",
    "/resourceregistry/api/v1/resource/{resourceId}")]
    [InlineData(
    "/api/healthcheck",
    "/api/healthcheck")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void NormalizeUrlPath_ShouldReplaceIdsCorrectly(string input, string expected)
    {
        // Act
        var result = HttpClientActivityEnricher.NormalizeUrlPath(input);

        // Assert
        Assert.Equal(expected, result);
    }
}