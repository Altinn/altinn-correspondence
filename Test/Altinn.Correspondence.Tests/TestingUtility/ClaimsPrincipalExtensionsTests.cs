using Altinn.Correspondence.Common.Helpers;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.TestingUtility
{
    public class ClaimsPrincipalExtensionsTests
    {
        [Fact]
        public void GetCallerPartyUrn_WithDialogTokenClaim_ReturnsPartyUrn()
        {
            // Arrange
            const string partyUrn = "urn:altinn:person:identifier-no:18865196381";

            var claims = new[]
            {
                new Claim("jti", "f166b031-41cc-4f1d-9ed6-f9b069ac91fd"),
                new Claim("c", partyUrn),
                new Claim("l", "3"),
                new Claim("p", partyUrn),
                new Claim("s", "urn:altinn:resource:bruno-correspondence"),
                new Claim("i", "019ae390-34e9-78da-b173-0e8c0244021d"),
                new Claim("a", "read;write"),
                new Claim("iss", "https://platform.tt02.altinn.no/dialogporten/api/v1"),
                new Claim("iat", "1764757554"),
                new Claim("nbf", "1764757554"),
                new Claim("exp", "1764758154")
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

            // Act
            var result = principal.GetCallerPartyUrn();

            // Assert
            Assert.Equal(partyUrn, result);
        }

        [Fact]
        public void GetCallerPartyUrn_WithoutPartyUrnClaim_ReturnsNull()
        {
            // Arrange
            var claims = new[]
            {
                new Claim("jti", "f166b031-41cc-4f1d-9ed6-f9b069ac91fd"),
                new Claim("l", "3"),
                new Claim("p", "urn:altinn:person:identifier-no:18865196381"),
                new Claim("s", "urn:altinn:resource:bruno-correspondence"),
                new Claim("i", "019ae390-34e9-78da-b173-0e8c0244021d"),
                new Claim("a", "read;write"),
                new Claim("iss", "https://platform.tt02.altinn.no/dialogporten/api/v1"),
                new Claim("iat", "1764757554"),
                new Claim("nbf", "1764757554"),
                new Claim("exp", "1764758154")
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

            // Act
            var result = principal.GetCallerPartyUrn();

            // Assert
            Assert.Null(result);
        }
    }
}
