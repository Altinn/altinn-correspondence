using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Register;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Idporten;
using Altinn.Platform.Register.Models;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Authorization;

public class IdportenXacmlMapperTests
{
    [Fact]
    public async Task CreateIdPortenDecisionRequest_WhenPrincipalHasManyClaims_UsesOnlyPidAsAccessSubjectIdentifier()
    {
        // Arrange
        const string tokenIssuer = "https://issuer.example.test/openid/";
        const string pid = "11998877665"; // not a real PID

        var claims = new List<Claim>
        {
            new("iss", tokenIssuer, ClaimValueTypes.String, tokenIssuer),
            new("nameid", "9001001", ClaimValueTypes.String, tokenIssuer),
            new(UrnConstants.UserId, "9001001", ClaimValueTypes.String, tokenIssuer),
            new("urn:altinn:username", "unit-test-user-42", ClaimValueTypes.String, tokenIssuer),
            new(UrnConstants.Party, "urn:altinn:partyid:70070070", ClaimValueTypes.String, tokenIssuer),
            new(UrnConstants.PartyUuid, "urn:altinn:party:uuid:11111111-2222-3333-4444-555555555555", ClaimValueTypes.String, tokenIssuer),
            new(UrnConstants.AuthenticationLevel, "4", ClaimValueTypes.String, tokenIssuer),
            new("acr", "idporten-loa-high", ClaimValueTypes.String, tokenIssuer),
            new("scope", "altinn:correspondence.read openid", ClaimValueTypes.String, tokenIssuer),
            new("client_id", "00000000-0000-0000-0000-000000000042", ClaimValueTypes.String, tokenIssuer),
            new("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:999888777\"}", ClaimValueTypes.String, tokenIssuer),
            new("pid", pid, ClaimValueTypes.String, tokenIssuer),
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var registerService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);

        // Act
        var requestRoot = await IdportenXacmlMapper.CreateIdPortenDecisionRequest(
            user,
            registerService.Object,
            actionTypes: ["read"],
            resourceId: "unit-test-resource",
            party: "999888777",
            instanceId: "unit-test-instance");

        // Assert
        Assert.NotNull(requestRoot);
        Assert.NotNull(requestRoot.Request);
        Assert.NotNull(requestRoot.Request.AccessSubject);
        Assert.Single(requestRoot.Request.AccessSubject);

        var subject = requestRoot.Request.AccessSubject[0];
        Assert.NotNull(subject.Attribute);
        Assert.Single(subject.Attribute);

        var subjectAttr = subject.Attribute[0];
        Assert.Equal(UrnConstants.PersonIdAttribute, subjectAttr.AttributeId);
        Assert.Equal(pid, subjectAttr.Value);

        Assert.DoesNotContain(subject.Attribute, a => a.AttributeId == UrnConstants.UserId);
    }

    [Fact]
    public async Task CreateIdPortenDecisionRequest_WhenPrincipalHasOnlyEmailClaim_ResolvesUserIdAndUsesItAsAccessSubject()
    {
        const string tokenIssuer = "https://test.idporten.no";
        const string email = "test@test.no";
        const int userId = 50;

        var claims = new List<Claim>
        {
            new("iss", tokenIssuer, ClaimValueTypes.String, tokenIssuer),
            new("sub", "0K8ZrC1DzgfH4AUOgP6CDW-IOTGwTElLBkvIU7N89Or0qYN0aM7h6UaX45rWbZrgxn4OXcYPPNMyqLMQBVojl9UwMADvhUMt4g", ClaimValueTypes.String, tokenIssuer),
            new("amr", "Selfregistered-email", ClaimValueTypes.String, tokenIssuer),
            new("acr", "selfregistered-email", ClaimValueTypes.String, tokenIssuer),
            new("email", email, ClaimValueTypes.String, tokenIssuer),
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var registerService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var emailUrn = $"{UrnConstants.PersonIdPortenEmailAttribute}:{email}";
        registerService
            .Setup(x => x.LookUpPartyV2ById(emailUrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartyV2 { PartyId = 12345678, User = new PartyUser()
            {
                UserId = userId
            }
            });

        var requestRoot = await IdportenXacmlMapper.CreateIdPortenDecisionRequest(
            user,
            registerService.Object,
            actionTypes: ["read"],
            resourceId: "unit-test-resource",
            party: "999888777",
            instanceId: null);

        Assert.NotNull(requestRoot?.Request?.AccessSubject);
        var subject = requestRoot.Request.AccessSubject[0];
        Assert.Single(subject.Attribute);
        Assert.Equal(UrnConstants.UserId, subject.Attribute[0].AttributeId);
        Assert.Equal(userId.ToString(), subject.Attribute[0].Value);
    }

    [Fact]
    public async Task CreateIdPortenDecisionRequest_WhenPrincipalHasOnlyEmailClaimAndRegisterReturnsNoUser_AccessSubjectIsEmpty()
    {
        const string tokenIssuer = "https://test.idporten.no";
        const string email = "unknown@test.no";

        var claims = new List<Claim>
        {
            new("iss", tokenIssuer, ClaimValueTypes.String, tokenIssuer),
            new("email", email, ClaimValueTypes.String, tokenIssuer),
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var registerService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var emailUrn = $"{UrnConstants.PersonIdPortenEmailAttribute}:{email}";
        registerService
            .Setup(x => x.LookUpPartyById(emailUrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Party?)null);

        var requestRoot = await IdportenXacmlMapper.CreateIdPortenDecisionRequest(
            user,
            registerService.Object,
            actionTypes: ["read"],
            resourceId: "unit-test-resource",
            party: "999888777",
            instanceId: null);

        Assert.NotNull(requestRoot?.Request?.AccessSubject);
        var subject = requestRoot.Request.AccessSubject[0];
        Assert.NotNull(subject.Attribute);
        Assert.Empty(subject.Attribute);
    }

    [Fact]
    public async Task CreateIdPortenDecisionRequest_WhenPrincipalHasNeitherPidNorEmail_Throws()
    {
        const string tokenIssuer = "https://test.idporten.no";
        var claims = new List<Claim>
        {
            new("iss", tokenIssuer, ClaimValueTypes.String, tokenIssuer),
            new("sub", "some-sub", ClaimValueTypes.String, tokenIssuer),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var registerService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);

        await Assert.ThrowsAsync<SecurityTokenException>(() =>
            IdportenXacmlMapper.CreateIdPortenDecisionRequest(
                user,
                registerService.Object,
                actionTypes: ["read"],
                resourceId: "unit-test-resource",
                party: "999888777",
                instanceId: null));
    }
}

