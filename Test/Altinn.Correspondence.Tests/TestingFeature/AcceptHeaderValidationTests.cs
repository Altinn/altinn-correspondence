using System.Net;
using System.Net.Http.Json;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;

namespace Altinn.Correspondence.Tests.TestingFeature;


[Collection(nameof(CustomWebApplicationTestsCollection))]
public class CorrespondenceInitializationTests : CorrespondenceTestBase
{
    public CorrespondenceInitializationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task InitializeEndpoint_WithValidAcceptHeader_ReturnsOk()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();
        _senderClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeEndpoint_WithNoAcceptHeader_ReturnsNotAcceptable()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeEndpoint_WithInvalidAcceptHeader_ReturnsNotAcceptable()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();
        _senderClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, initializeCorrespondenceResponse.StatusCode);
    }
}