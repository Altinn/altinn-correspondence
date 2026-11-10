using System.Net;
using System.Text;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Altinn.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Altinn;

public class AltinnEventBusTests
{
    private const string ResourceId = "test-resource";
    private const string ItemId = "11111111-1111-1111-1111-111111111111";

    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();

    [Fact]
    public async Task Publish_ShouldThrow_WhenEventsApiReturnsErrorStatusCode()
    {
        // Arrange
        SetupPublishEventResponse(HttpStatusCode.ServiceUnavailable, "Events is down");
        var eventBus = CreateEventBus();

        // Act
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            eventBus.Publish(AltinnEventType.CorrespondencePublished, ResourceId, ItemId, "correspondence", cancellationToken: CancellationToken.None));

        // Assert
        Assert.Contains("Error when publishing cloud event CorrespondencePublished", exception.Message);
        Assert.Contains(ItemId, exception.Message);
        Assert.Contains("ServiceUnavailable", exception.Message);
        Assert.Contains("Events is down", exception.Message);
    }

    [Fact]
    public async Task Publish_ShouldComplete_WhenEventsApiReturnsSuccessStatusCode()
    {
        // Arrange
        SetupPublishEventResponse(HttpStatusCode.OK, string.Empty);
        var eventBus = CreateEventBus();

        // Act
        await eventBus.Publish(AltinnEventType.CorrespondencePublished, ResourceId, ItemId, "correspondence", cancellationToken: CancellationToken.None);

        // Assert
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request => request.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>());
    }

    private AltinnEventBus CreateEventBus()
    {
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://platform.example.test/")
        };

        var generalSettings = Options.Create(new GeneralSettings
        {
            CorrespondenceBaseUrl = "https://correspondence.example.test/"
        });

        return new AltinnEventBus(httpClient, generalSettings, Mock.Of<ILogger<AltinnEventBus>>());
    }

    private void SetupPublishEventResponse(HttpStatusCode statusCode, string responseBody)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Post
                    && request.RequestUri!.ToString().Contains("events/api/v1/events", StringComparison.Ordinal)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
    }
}
