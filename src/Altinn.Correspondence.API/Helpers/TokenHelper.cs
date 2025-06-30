using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Common.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Altinn.Correspondence.API.Helpers
{
    public static class TokenHelper
    {
        public static AuthorizationPolicyBuilder RequireScopeIfAltinn(this AuthorizationPolicyBuilder authorizationPolicyBuilder, IConfiguration config, params string[] scopes) => 
            authorizationPolicyBuilder.RequireAssertion(context =>
            {
                var altinnOptions = new AltinnOptions();
                config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
                bool isAltinnToken = context.User.HasClaim(c => c.Issuer == $"{altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/openid/");
                if (isAltinnToken)
                {
                    return context.User.HasClaim(c => c.Type == "scope" && scopes.Intersect(c.Value.Split(' ')).Any());
                }
                return true;            
            });

        /// <summary>
        /// Require scopes by token issuer. 
        /// Determines token type by examining the issuer claim (maskinporten.no vs altinn.no)
        /// For Maskinporten tokens: requires BOTH altinn:serviceowner AND the specified scope
        /// For Altinn tokens: only requires the specified scope
        /// </summary>
        /// <param name="authorizationPolicyBuilder">The authorization policy builder.</param>
        /// <param name="altinnScope">The altinn scope.</param>
        /// <param name="maskinportenRequiredScope">The maskinporten required scope.</param>
        /// <returns>The authorization policy builder.</returns>
        public static AuthorizationPolicyBuilder RequireScopesByTokenIssuer(this AuthorizationPolicyBuilder authorizationPolicyBuilder, string altinnScope, string maskinportenRequiredScope = null) =>
            authorizationPolicyBuilder.RequireAssertion(context =>
            {
                // Check if this is a Maskinporten token by checking the issuer
                var issuerClaim = context.User.Claims.FirstOrDefault(c => c.Type == "iss");
                bool isMaskinportenToken = issuerClaim?.Value.Contains(AuthorizationConstants.MaskinportenIssuer) ?? false;
                bool isAltinnToken = issuerClaim?.Value.Contains(AuthorizationConstants.AltinnIssuer) ?? false;
                
                // Get all scope claims (handles both single and space-separated formats)
                var scopeClaims = context.User.Claims.Where(c => c.Type == "scope").Select(c => c.Value);
                var allScopes = new List<string>();
                
                foreach (var scopeClaimValue in scopeClaims)
                {
                    allScopes.AddRange(scopeClaimValue.Split(' '));
                }
                
                if (isMaskinportenToken)
                {
                    // For Maskinporten tokens: require BOTH serviceowner scope AND the specified scope
                    if (maskinportenRequiredScope != null)
                    {
                        return allScopes.Contains(AuthorizationConstants.ServiceOwnerScope) && 
                               allScopes.Contains(maskinportenRequiredScope);
                    }
                    return allScopes.Contains(AuthorizationConstants.ServiceOwnerScope) && 
                           allScopes.Contains(altinnScope);
                }
                else if (isAltinnToken)
                {
                    // For Altinn tokens: only require the specified scope
                    return allScopes.Contains(altinnScope);
                }
                return false;
            });
    }

}
