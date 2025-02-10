using System.Net;
using System.Net.Http.Headers;
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
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeEndpoint_WithNoAcceptHeader_ReturnsOk()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeEndpoint_WithInvalidAcceptHeader_ReturnsNotAcceptable()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeEndpoint_WithValidAcceptHeaderAmongMultipleAcceptHeadersWithQualityFactors_ReturnsOk()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain", 0.9));
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/markdown", 0.8));
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.5));
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
    }
}