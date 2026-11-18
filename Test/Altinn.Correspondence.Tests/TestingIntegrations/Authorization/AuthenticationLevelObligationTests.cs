using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Idporten;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Authorization;

public class AuthenticationLevelObligationTests
{
    private const string DialogportenIssuer = "https://dialogporten.example.test/api/v1";
    private const string IdportenIssuer = "https://test.idporten.no";
    private const string AltinnIssuer = "https://platform.altinn.no/authentication/api/v1/openid/";
    private const string Level4 = "4";
    private const string Level3 = "3";

    [Fact]
    public void Parse_ReturnsGeneralAuthenticationLevel_AndIgnoresOrgAndSystemUserObligations()
    {
        var xml = File.ReadAllText("Data/altinn-correspondence-test-resource-1.xml");

        var parsed = GetResourcePolicyAuthLevelResponse.Parse(xml);

        Assert.Equal([4], parsed.MinimumAuthenticationLevels);
    }

    [Fact]
    public void Parse_WhenOnlySystemUserObligationIsPresent_ReturnsNoGeneralLevel()
    {
        const string xml = """
            <xacml:Policy xmlns:xacml="urn:oasis:names:tc:xacml:3.0:core:schema:wd-17">
              <xacml:ObligationExpressions>
                <xacml:ObligationExpression FulfillOn="Permit" ObligationId="urn:altinn:obligation:3">
                  <xacml:AttributeAssignmentExpression AttributeId="urn:altinn:obligation-assignment:3" Category="urn:altinn:minimum-authenticationlevel-systemuser">
                    <xacml:AttributeValue DataType="http://www.w3.org/2001/XMLSchema#integer">3</xacml:AttributeValue>
                  </xacml:AttributeAssignmentExpression>
                </xacml:ObligationExpression>
              </xacml:ObligationExpressions>
            </xacml:Policy>
            """;

        var parsed = GetResourcePolicyAuthLevelResponse.Parse(xml);

        Assert.Empty(parsed.MinimumAuthenticationLevels);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AltinnToken_SystemUserOnLevel4Resource_IsAllowedOnlyWhenSystemUserObligationIsPresent(bool includeSystemUserObligation)
    {
        var user = Principal(
            new Claim("iss", AltinnIssuer),
            new Claim(UrnConstants.AuthenticationLevel, Level3),
            SystemUserAuthorizationDetailsClaim());

        var allowed = await CheckAccess(user, PermitWithAuthenticationLevel(Level4, includeSystemUserObligation));

        Assert.Equal(includeSystemUserObligation, allowed);
    }

    [Fact]
    public void AltinnToken_SystemUserWithoutAuthLevelClaim_IsTreatedAsLevel3WhenObligationIsPresent()
    {
        var user = Principal(SystemUserAuthorizationDetailsClaim());
        var result = DecisionHelper.ValidateDecisionResult(PermitResult(Level4, includeSystemUserObligation: true), user);

        Assert.True(result);
    }

    [Fact]
    public void AltinnToken_EndUserAtLevel3_IsRejectedOnLevel4ResourceEvenWhenSystemUserObligationIsPresent()
    {
        var user = Principal(new Claim(UrnConstants.AuthenticationLevel, Level3));
        var result = DecisionHelper.ValidateDecisionResult(PermitResult(Level4, includeSystemUserObligation: true), user);

        Assert.False(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DialogToken_Level3_IsRejectedOnLevel4ResourceRegardlessOfSystemUserObligation(bool includeSystemUserObligation)
    {
        var user = Principal(new Claim("l", Level3), new Claim("y", "urn:altinn:systemuser:uuid:0194a1b2-3c4d-7e5f-8a9b-0c1d2e3f4a5b"));
        var allowed = DialogTokenXacmlMapper.ValidateDialogportenResult(PermitResponse(Level4, includeSystemUserObligation), user);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData("idporten-loa-substantial", false)]
    [InlineData("idporten-loa-high", true)]
    public void IdportenToken_SystemUserObligationDoesNotChangeTheOutcome(string acr, bool expected)
    {
        var user = Principal(new Claim("http://schemas.microsoft.com/claims/authnclassreference", acr));
        var allowed = IdportenXacmlMapper.ValidateIdportenAuthorizationResponse(
            PermitResponse(Level4, includeSystemUserObligation: true),
            user);

        Assert.Equal(expected, allowed);
    }

    [Fact]
    public async Task DialogToken_IsNotTreatedAsSystemUserWhenSystemUserObligationIsPresent()
    {
        var user = Principal(
            new Claim("iss", DialogportenIssuer),
            new Claim("y", "urn:altinn:systemuser:uuid:0194a1b2-3c4d-7e5f-8a9b-0c1d2e3f4a5b"),
            new Claim("l", Level3),
            new Claim("a", "read"));

        var allowed = await CheckAccess(user, PermitWithAuthenticationLevel(Level4, includeSystemUserObligation: true));

        Assert.False(allowed);
    }

    private static async Task<bool> CheckAccess(ClaimsPrincipal user, string pdpResponseJson)
    {
        var handler = new StaticPdpResponseHandler(pdpResponseJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var resourceRegistry = new Mock<IResourceRegistryService>(MockBehavior.Strict);
        resourceRegistry
            .Setup(x => x.GetServiceOwnerNameOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("unit-test-service-owner");

        var sut = new AltinnAuthorizationService(
            httpClient,
            Options.Create(new AltinnOptions { PlatformSubscriptionKey = "dummy", PlatformGatewayUrl = "https://unit.test/" }),
            Options.Create(new DialogportenSettings { Issuer = DialogportenIssuer }),
            Options.Create(new IdportenSettings { Issuer = IdportenIssuer }),
            resourceRegistry.Object,
            new Mock<IAltinnRegisterService>(MockBehavior.Strict).Object,
            new Mock<ILogger<AltinnAuthorizationService>>().Object);

        return await sut.CheckAccessAsAny(user, "unit-test-resource", "50000001", CancellationToken.None);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "TestAuth"));

    private static Claim SystemUserAuthorizationDetailsClaim()
    {
        var details = new SystemUserAuthorizationDetails
        {
            Type = UrnConstants.SystemUser,
            SystemUserOrg = new SystemUserOrg
            {
                Authority = "iso6523-actorid-upis",
                ID = "0192:991825827"
            },
            SystemUserId = ["0194a1b2-3c4d-7e5f-8a9b-0c1d2e3f4a5b"],
            SystemId = "991825827_correspondencesystem"
        };
        return new Claim("authorization_details", JsonSerializer.Serialize(details));
    }

    private static XacmlJsonResult PermitResult(string generalLevel, bool includeSystemUserObligation, string systemUserLevel = Level3)
    {
        var obligations = new List<XacmlJsonObligationOrAdvice>
        {
            Obligation(UrnConstants.MinimumAuthenticationLevel, generalLevel)
        };
        if (includeSystemUserObligation)
        {
            obligations.Add(Obligation(UrnConstants.MinimumAuthenticationLevelSystemUser, systemUserLevel));
        }

        return new XacmlJsonResult
        {
            Decision = "Permit",
            Obligations = obligations
        };
    }

    private static XacmlJsonResponse PermitResponse(string generalLevel, bool includeSystemUserObligation, string systemUserLevel = Level3)
    {
        return new XacmlJsonResponse
        {
            Response = [PermitResult(generalLevel, includeSystemUserObligation, systemUserLevel)]
        };
    }

    private static XacmlJsonObligationOrAdvice Obligation(string category, string value) =>
        new()
        {
            AttributeAssignment =
            [
                new XacmlJsonAttributeAssignment
                {
                    Category = category,
                    Value = value,
                    AttributeId = "urn:altinn:obligation-assignment:test"
                }
            ]
        };

    private static string PermitWithAuthenticationLevel(string generalLevel, bool includeSystemUserObligation)
    {
        var systemUserObligation = includeSystemUserObligation
            ? """,{"Id":"urn:altinn:obligation:3","AttributeAssignment":[{"AttributeId":"urn:altinn:obligation-assignment:3","Value":"3","Category":"urn:altinn:minimum-authenticationlevel-systemuser","DataType":"http://www.w3.org/2001/XMLSchema#integer"}]}"""
            : string.Empty;

        return $$"""
            {"Response":[{"Decision":"Permit","Obligations":[{"Id":"urn:altinn:obligation:1","AttributeAssignment":[{"AttributeId":"urn:altinn:obligation-assignment:1","Value":"{{generalLevel}}","Category":"urn:altinn:minimum-authenticationlevel","DataType":"http://www.w3.org/2001/XMLSchema#integer"}]}{{systemUserObligation}}]}]}
            """;
    }

    private sealed class StaticPdpResponseHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
