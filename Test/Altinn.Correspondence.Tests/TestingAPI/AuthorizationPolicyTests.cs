using System.Security.Claims;
using Altinn.Correspondence.API.Auth;
using Altinn.Correspondence.Common.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Altinn.Correspondence.Tests.TestingAPI;

public class AuthorizationPolicyTests
{
    [Theory]
    [InlineData("https://platform.at23.altinn.cloud")]
    [InlineData("https://platform.yt01.altinn.cloud")]
    [InlineData("https://platform.tt02.altinn.no")]
    [InlineData("https://platform.tt02.altinn.no/")]
    public void RecipientScopePolicy_AcceptsConfiguredAltinnIssuer(string platformGatewayUrl)
    {
        var context = CreateContext(
            $"{platformGatewayUrl.TrimEnd('/')}/authentication/api/v1/openid/",
            AuthorizationConstants.RecipientScope);

        var authorized = DependencyInjection.RecipientScopePolicy(context, platformGatewayUrl);

        Assert.True(authorized);
    }

    [Fact]
    public void RecipientScopePolicy_RejectsDifferentAltinnIssuer()
    {
        var context = CreateContext(
            "https://platform.other.altinn.cloud/authentication/api/v1/openid/",
            AuthorizationConstants.RecipientScope);

        var authorized = DependencyInjection.RecipientScopePolicy(
            context,
            "https://platform.at23.altinn.cloud");

        Assert.False(authorized);
    }

    [Fact]
    public void RecipientScopePolicy_RejectsMissingRecipientScope()
    {
        var platformGatewayUrl = "https://platform.at23.altinn.cloud";
        var context = CreateContext(
            $"{platformGatewayUrl}/authentication/api/v1/openid/",
            AuthorizationConstants.SenderScope);

        var authorized = DependencyInjection.RecipientScopePolicy(context, platformGatewayUrl);

        Assert.False(authorized);
    }

    private static AuthorizationHandlerContext CreateContext(string issuer, string scope)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", issuer),
            new Claim("scope", scope),
        ], "Test"));

        return new AuthorizationHandlerContext([], principal, resource: null);
    }
}
