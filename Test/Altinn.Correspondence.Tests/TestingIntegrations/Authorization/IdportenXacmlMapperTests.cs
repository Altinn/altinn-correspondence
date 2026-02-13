using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Idporten;
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
}

