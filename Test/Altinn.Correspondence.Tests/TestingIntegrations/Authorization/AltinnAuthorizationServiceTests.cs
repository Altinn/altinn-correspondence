using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
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
            new("iss", idportenIssuer, ClaimValueTypes.String, idportenIssuer),
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

    [Fact]
    public async Task CheckAccessAsAny_WhenIdportenEmailTokenAndRegisterReturnsUser_UsesUserIdInAccessSubject()
    {
        const string idportenIssuer = "https://test.idporten.no";
        const string email = "test@test.no";
        const int userId = 50;

        var userClaims = new List<Claim>
        {
            new("iss", idportenIssuer, ClaimValueTypes.String, idportenIssuer),
            new("sub", "0K8ZrC1DzgfH4AUOgP6CDW-IOTGwTElLBkvIU7N89Or0qYN0aM7h6UaX45rWbZrgxn4OXcYPPNMyqLMQBVojl9UwMADvhUMt4g", ClaimValueTypes.String, idportenIssuer),
            new("acr", "selfregistered-email", ClaimValueTypes.String, idportenIssuer),
            new("http://schemas.microsoft.com/claims/authnclassreference", "selfregistered-email", ClaimValueTypes.String, idportenIssuer),
            new("email", email, ClaimValueTypes.String, idportenIssuer),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(userClaims, "TestAuth"));

        var captureHandler = new CapturePdpRequestHandler();
        var httpClient = new HttpClient(captureHandler) { BaseAddress = new Uri("https://unit.test/") };
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var altinnOptions = Options.Create(new AltinnOptions { PlatformSubscriptionKey = "dummy", PlatformGatewayUrl = "https://unit.test/" });
        var dialogportenSettings = Options.Create(new DialogportenSettings { Issuer = "https://dialogporten.example.test/api/v1" });
        var idportenSettings = Options.Create(new IdportenSettings { Issuer = idportenIssuer });

        var resourceRegistry = new Mock<IResourceRegistryService>(MockBehavior.Strict);
        resourceRegistry.Setup(x => x.GetServiceOwnerNameOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("unit-test-service-owner");

        var registerService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var emailUrn = $"{UrnConstants.PersonIdPortenEmailAttribute}:{email}";
        registerService.Setup(x => x.LookUpPartyById(emailUrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party { PartyId = 12345678, UserId = userId });

        var sut = new AltinnAuthorizationService(
            httpClient, altinnOptions, dialogportenSettings, idportenSettings,
            resourceRegistry.Object, registerService.Object, new Mock<ILogger<AltinnAuthorizationService>>().Object);

        var allowed = await sut.CheckAccessAsAny(user, resource: "unit-test-resource", party: "999888777", cancellationToken: CancellationToken.None);

        Assert.True(allowed);
        Assert.NotNull(captureHandler.LastRequestJson);
        using var doc = JsonDocument.Parse(captureHandler.LastRequestJson);
        var accessSubjectAttributes = doc.RootElement.GetProperty("request").GetProperty("accessSubject")[0].GetProperty("attribute").EnumerateArray()
            .Select(a => (a.GetProperty("attributeId").GetString(), a.GetProperty("value").GetString())).ToList();
        Assert.Single(accessSubjectAttributes);
        Assert.Equal(UrnConstants.UserId, accessSubjectAttributes[0].Item1);
        Assert.Equal(userId.ToString(), accessSubjectAttributes[0].Item2);
    }

    [Fact]
    public async Task CheckAccessAsAny_WhenIdportenEmailTokenAndRegisterReturnsNoUser_AccessSubjectIsEmpty()
    {
        const string idportenIssuer = "https://test.idporten.no";
        const string email = "unknown@test.no";

        var userClaims = new List<Claim>
        {
            new("iss", idportenIssuer, ClaimValueTypes.String, idportenIssuer),
            new("email", email, ClaimValueTypes.String, idportenIssuer),
            new("acr", "selfregistered-email", ClaimValueTypes.String, idportenIssuer),
            new("http://schemas.microsoft.com/claims/authnclassreference", "selfregistered-email", ClaimValueTypes.String, idportenIssuer),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(userClaims, "TestAuth"));

        var captureHandler = new CapturePdpRequestHandler();
        var httpClient = new HttpClient(captureHandler) { BaseAddress = new Uri("https://unit.test/") };
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var altinnOptions = Options.Create(new AltinnOptions { PlatformSubscriptionKey = "dummy", PlatformGatewayUrl = "https://unit.test/" });
        var dialogportenSettings = Options.Create(new DialogportenSettings { Issuer = "https://dialogporten.example.test/api/v1" });
        var idportenSettings = Options.Create(new IdportenSettings { Issuer = idportenIssuer });

        var resourceRegistry = new Mock<IResourceRegistryService>(MockBehavior.Strict);
        resourceRegistry.Setup(x => x.GetServiceOwnerNameOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("unit-test-service-owner");

        var registerService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var emailUrn = $"{UrnConstants.PersonIdPortenEmailAttribute}:{email}";
        registerService.Setup(x => x.LookUpPartyById(emailUrn, It.IsAny<CancellationToken>())).ReturnsAsync((Party?)null);

        var sut = new AltinnAuthorizationService(
            httpClient, altinnOptions, dialogportenSettings, idportenSettings,
            resourceRegistry.Object, registerService.Object, new Mock<ILogger<AltinnAuthorizationService>>().Object);

        await sut.CheckAccessAsAny(user, resource: "unit-test-resource", party: "999888777", cancellationToken: CancellationToken.None);

        Assert.NotNull(captureHandler.LastRequestJson);
        using var doc = JsonDocument.Parse(captureHandler.LastRequestJson);
        var attributes = doc.RootElement.GetProperty("request").GetProperty("accessSubject")[0].GetProperty("attribute");
        Assert.Equal(0, attributes.GetArrayLength());
    }

    [Fact]
    public async Task CheckAccessAsAny_WhenPartyIsIdportenEmailUrn_ResolvesToPartyIdInResourceCategory()
    {
        const string idportenIssuer = "https://test.idporten.no";
        const string emailUrn = "urn:altinn:person:idporten-email:recipient@example.com";
        const int resolvedPartyId = 50952483;

        var userClaims = new List<Claim>
        {
            new("iss", idportenIssuer, ClaimValueTypes.String, idportenIssuer),
            new("pid", "01018045678", ClaimValueTypes.String, idportenIssuer),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(userClaims, "TestAuth"));

        var captureHandler = new CapturePdpRequestHandler();
        var httpClient = new HttpClient(captureHandler) { BaseAddress = new Uri("https://unit.test/") };
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var altinnOptions = Options.Create(new AltinnOptions { PlatformSubscriptionKey = "dummy", PlatformGatewayUrl = "https://unit.test/" });
        var dialogportenSettings = Options.Create(new DialogportenSettings { Issuer = "https://dialogporten.example.test/api/v1" });
        var idportenSettings = Options.Create(new IdportenSettings { Issuer = idportenIssuer });

        var resourceRegistry = new Mock<IResourceRegistryService>(MockBehavior.Strict);
        resourceRegistry.Setup(x => x.GetServiceOwnerNameOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("unit-test-service-owner");

        var registerService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        registerService.Setup(x => x.LookUpPartyById(emailUrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party { PartyId = resolvedPartyId, PartyUuid = Guid.NewGuid() });

        var sut = new AltinnAuthorizationService(
            httpClient, altinnOptions, dialogportenSettings, idportenSettings,
            resourceRegistry.Object, registerService.Object, new Mock<ILogger<AltinnAuthorizationService>>().Object);

        var allowed = await sut.CheckAccessAsAny(user, resource: "unit-test-resource", party: emailUrn, cancellationToken: CancellationToken.None);

        Assert.True(allowed);
        Assert.NotNull(captureHandler.LastRequestJson);
        using var doc = JsonDocument.Parse(captureHandler.LastRequestJson);
        var resourceAttributes = doc.RootElement.GetProperty("request").GetProperty("resource").EnumerateArray()
            .SelectMany(r => r.GetProperty("attribute").EnumerateArray())
            .Where(a => a.GetProperty("attributeId").GetString() == UrnConstants.Party)
            .ToList();
        Assert.Single(resourceAttributes);
        Assert.Equal(resolvedPartyId.ToString(), resourceAttributes[0].GetProperty("value").GetString());
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

