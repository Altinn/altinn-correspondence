using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Authorization;

public class AltinnAuthorizationServiceTests
{
    [Fact]
    public async Task CheckAccessAsAny_WhenPidClaimIssuerMatchesIdportenSettings_UsesIdportenDecisionRequest()
    {
        // Arrange
        const string idportenIssuer = "https://issuer.example.test/openid/";
        const string pid = "55001122334"; // not a real PID

        var userClaims = new List<Claim>
        {
            new("nameid", "8002002", ClaimValueTypes.String, idportenIssuer),
            new("urn:altinn:userid", "8002002", ClaimValueTypes.String, idportenIssuer),
            new("urn:altinn:username", "unit-test-user-99", ClaimValueTypes.String, idportenIssuer),
            new("scope", "altinn:correspondence.read openid", ClaimValueTypes.String, idportenIssuer),
            new("acr", "idporten-loa-high", ClaimValueTypes.String, idportenIssuer),
            new("http://schemas.microsoft.com/claims/authnclassreference", "idporten-loa-high", ClaimValueTypes.String, idportenIssuer),
            new("pid", pid, ClaimValueTypes.String, idportenIssuer),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(userClaims, "TestAuth"));

        var captureHandler = new CapturePdpRequestHandler();
        var httpClient = new HttpClient(captureHandler)
        {
            BaseAddress = new Uri("https://unit.test/")
        };
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var altinnOptions = Options.Create(new AltinnOptions
        {
            PlatformSubscriptionKey = "dummy-subscription-key",
            PlatformGatewayUrl = "https://unit.test/"
        });
        var dialogportenSettings = Options.Create(new DialogportenSettings
        {
            Issuer = "https://dialogporten.example.test/api/v1"
        });
        var idportenSettings = Options.Create(new IdportenSettings
        {
            Issuer = idportenIssuer
        });

        var resourceRegistry = new Mock<IResourceRegistryService>(MockBehavior.Strict);
        resourceRegistry
            .Setup(x => x.GetServiceOwnerNameOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("unit-test-service-owner");

        var logger = new Mock<ILogger<AltinnAuthorizationService>>();

        var registerService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);

        var sut = new AltinnAuthorizationService(
            httpClient,
            altinnOptions,
            dialogportenSettings,
            idportenSettings,
            resourceRegistry.Object,
            registerService.Object,
            logger.Object);

        // Act
        var allowed = await sut.CheckAccessAsAny(
            user,
            resource: "unit-test-resource",
            party: "999888777",
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.True(allowed);
        Assert.NotNull(captureHandler.LastRequestJson);

        using var doc = JsonDocument.Parse(captureHandler.LastRequestJson);
        var accessSubjectAttributes = doc.RootElement
            .GetProperty("request")
            .GetProperty("accessSubject")[0]
            .GetProperty("attribute")
            .EnumerateArray()
            .Select(a => a.GetProperty("attributeId").GetString())
            .ToList();

        Assert.Single(accessSubjectAttributes);
        Assert.Equal(UrnConstants.PersonIdAttribute, accessSubjectAttributes[0]);

        // check that the altinn user id was not forwarded to AccessSubject (which would happen with other mappers).
        Assert.DoesNotContain("urn:altinn:userid", captureHandler.LastRequestJson, StringComparison.Ordinal);
    }

    private sealed class CapturePdpRequestHandler : HttpMessageHandler
    {
        public string? LastRequestJson { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestJson = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            // Minimal Permit response.
            var responseJson = "{\"Response\":[{\"Decision\":\"Permit\"}]}";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}

