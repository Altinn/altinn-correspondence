using System.Net;
using System.Text;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Altinn.Register.Contracts;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Altinn;

public class AltinnRegisterServiceTests
{
    /// <summary>
    /// A syntactically valid URN that passes local normalization but is rejected by Register with "Invalid PartyUrn".
    /// </summary>
    private const string InvalidPartyUrnIdentifier = "urn:altinn:party:uuid:not-a-valid-party";

    private const string InvalidPartyUrnErrorJson = """
        {
          "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
          "title": "One or more validation errors occurred.",
          "status": 400,
          "errors": {
            "parties": [
              "The parties field is required."
            ],
            "$.data[0]": [
              "Invalid PartyUrn"
            ]
          },
          "traceId": "00-67b20a9cca33c5f030b9bf373bd070b1-00f08d96e905a6ac-00"
        }
        """;

    private const string OtherBadRequestErrorJson = """
        {
          "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
          "title": "One or more validation errors occurred.",
          "status": 400,
          "errors": {
            "parties": [
              "The parties field is required."
            ]
          },
          "traceId": "00-67b20a9cca33c5f030b9bf373bd070b1-00f08d96e905a6ac-00"
        }
        """;

    private const string SuccessfulQueryResponseJson = """
        {
          "data": [
            {
              "partyType": "organization",
              "organizationIdentifier": "313230090",
              "partyUuid": "8369b52e-88d6-4f94-aa41-1c53f7ebd986",
              "versionId": 585246755,
              "urn": "urn:altinn:party:uuid:8369b52e-88d6-4f94-aa41-1c53f7ebd986",
              "externalUrn": "urn:altinn:organization:identifier-no:313230090",
              "partyId": 51448661,
              "displayName": "OKSYDERT HES TIGER AS",
              "user": null
            }
          ]
        }
        """;

    private readonly Mock<ILogger<AltinnRegisterService>> _loggerMock = new();
    private readonly Mock<IHybridCacheWrapper> _cacheMock = new();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();

    public AltinnRegisterServiceTests()
    {
        SetupCacheMiss();
    }

    [Fact]
    public async Task LookUpPartyById_WhenInvalidPartyUrn_ReturnsNullAndLogsWarning()
    {
        SetupQueryPartiesResponse(HttpStatusCode.BadRequest, InvalidPartyUrnErrorJson);
        var service = CreateService();

        var result = await service.LookUpPartyById(InvalidPartyUrnIdentifier, CancellationToken.None);

        Assert.Null(result);
        VerifyWarningLogged("Bad input provided when querying parties in Altinn Register");
    }

    [Fact]
    public async Task LookUpPartiesByIds_WhenInvalidPartyUrn_ReturnsEmptyListAndLogsWarning()
    {
        SetupQueryPartiesResponse(HttpStatusCode.BadRequest, InvalidPartyUrnErrorJson);
        var service = CreateService();

        var result = await service.LookUpPartiesByIds([InvalidPartyUrnIdentifier], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
        VerifyWarningLogged("Bad input provided when querying parties in Altinn Register");
    }

    [Fact]
    public async Task LookUpPartyById_WhenBadRequestWithoutInvalidPartyUrn_Throws()
    {
        SetupQueryPartiesResponse(HttpStatusCode.BadRequest, OtherBadRequestErrorJson);
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.LookUpPartyById("313230090", CancellationToken.None));

        Assert.Contains("Error when querying parties in Altinn Register", exception.Message);
        VerifyWarningLogged("Bad input provided when querying parties in Altinn Register", Times.Never());
    }

    [Fact]
    public async Task LookUpPartyById_WhenQuerySucceeds_ReturnsParty()
    {
        SetupQueryPartiesResponse(HttpStatusCode.OK, SuccessfulQueryResponseJson);
        var service = CreateService();

        var result = await service.LookUpPartyById("313230090", CancellationToken.None);

        Assert.NotNull(result);
        Assert.IsType<Organization>(result);
        Assert.Equal("313230090", result.GetOrganizationIdentifier());
        Assert.Equal(Guid.Parse("8369b52e-88d6-4f94-aa41-1c53f7ebd986"), result.Uuid);
        Assert.Equal("Oksydert Hes Tiger As", result.GetDisplayName());
        Assert.Equal(51448661, result.GetPartyId());
    }

    [Fact]
    public async Task LookUpPartiesByIds_WhenQuerySucceeds_ReturnsParties()
    {
        SetupQueryPartiesResponse(HttpStatusCode.OK, SuccessfulQueryResponseJson);
        var service = CreateService();

        var result = await service.LookUpPartiesByIds(["313230090"], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("313230090", result[0].GetOrganizationIdentifier());
    }

    private AltinnRegisterService CreateService()
    {
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://platform.example.test/")
        };

        var altinnOptions = Options.Create(new AltinnOptions
        {
            PlatformSubscriptionKey = "test-subscription-key",
            PlatformGatewayUrl = "https://platform.example.test/"
        });

        return new AltinnRegisterService(httpClient, altinnOptions, _loggerMock.Object, _cacheMock.Object);
    }

    private void SetupCacheMiss()
    {
        _cacheMock
            .Setup(cache => cache.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, ValueTask<byte[]>>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<byte>());

        _cacheMock
            .Setup(cache => cache.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupQueryPartiesResponse(HttpStatusCode statusCode, string responseBody)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Post
                    && request.RequestUri!.ToString().Contains("parties/query", StringComparison.Ordinal)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
    }

    private void VerifyWarningLogged(string expectedMessageFragment, Times? times = null)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(expectedMessageFragment)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times ?? Times.Once());
    }
}
