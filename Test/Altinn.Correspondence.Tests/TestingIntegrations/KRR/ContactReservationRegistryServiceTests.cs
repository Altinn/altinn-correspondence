using System.Net;
using Altinn.Correspondence.Integrations.Altinn.ContactReservationRegistry;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Altinn.Correspondence.Tests.KRR;

public class ContactReservationRegistryServiceTests
{
    private readonly Mock<ILogger<ContactReservationRegistryService>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly ContactReservationRegistryService _service;

    public ContactReservationRegistryServiceTests()
    {
        _mockLogger = new Mock<ILogger<ContactReservationRegistryService>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://example.com")
        };
        _service = new ContactReservationRegistryService(_httpClient, _mockLogger.Object);
    }

    [Fact]
    public async Task GetReservedRecipients_WithTimeout_ThrowsHttpRequestException()
    {
        // Arrange
        var recipients = new List<string> { "12345678901" };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                // Simulate a delay longer than the 2-second timeout in order to get the correct exception message
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => 
            _service.GetReservedRecipients(recipients));
        
        Assert.Contains("Timeout while calling the KRR API", exception.Message);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Timeout while calling the KRR API after")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task GetReservedRecipients_WithinTimeLimit_ReturnsSuccess()
    {
        // Arrange
        var recipients = new List<string> { "12345678901" };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                var response = new ContactReservationPersonResponse
                {
                    Personer = new List<ContactReservationPerson>
                    {
                        new() { Personidentifikator = "12345678901", Reservasjon = "JA" }
                    }
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(response))
                };
            });

        // Act & Assert
        var result = await _service.GetReservedRecipients(recipients);
        
        Assert.Equal(recipients, result);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error while calling the KRR API")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never());
    }
}
