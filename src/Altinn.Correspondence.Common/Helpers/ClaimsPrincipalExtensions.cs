using Altinn.Correspondence.Common.Helpers.Models;
using System.Security.Claims;
using System.Text.Json;

namespace Altinn.Correspondence.Common.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetCallerOrganizationId(this ClaimsPrincipal user)
        {
            var claims = user.Claims;
            // System user token
            var systemUserClaim = user.Claims.FirstOrDefault(c => c.Type == "authorization_details");
            if (systemUserClaim is not null)
            {
                var systemUserAuthorizationDetails = JsonSerializer.Deserialize<SystemUserAuthorizationDetails>(systemUserClaim.Value);
                return systemUserAuthorizationDetails?.SystemUserOrg.ID.WithoutPrefix();
            }
            // Enterprise token
            var orgClaim = user.Claims.FirstOrDefault(c => c.Type == "urn:altinn:orgNumber");
            if (orgClaim is not null)
            {
                return orgClaim.Value.WithoutPrefix(); // Normalize to same format as elsewhere
            }
            // Personal token
            var consumerClaim = user.Claims.FirstOrDefault(c => c.Type == "consumer");
            if (consumerClaim is not null)
            {
                var consumerObject = JsonSerializer.Deserialize<TokenConsumer>(consumerClaim.Value);
                return consumerObject?.ID.WithoutPrefix();
            }
            // DialogToken
            var dialogportenTokenUserId = user.Claims.FirstOrDefault(c => c.Type == "p")?.Value;
            if (dialogportenTokenUserId is not null)
            {
                return dialogportenTokenUserId.WithoutPrefix(); // Normalize to same format as elsewhere
            }
            return null;
        }

        public static bool CallingAsSender(this ClaimsPrincipal user)
        {
            var scope = user.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
            return scope?.Contains("altinn:correspondence.write") ?? false;
        }
        public static bool CallingAsRecipient(this ClaimsPrincipal user)
        {
            var scope = user.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
            return scope?.Contains("altinn:correspondence.read") ?? false;
        }
    }
}
