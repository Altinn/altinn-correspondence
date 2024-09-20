using Altinn.Common.PEP.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.Helpers
{

    internal class MockPolicyEvaluator : IPolicyEvaluator
    {
        private List<Claim> _customClaims;
        private readonly List<Claim> _defaultClaims = new List<Claim>
        {
                new Claim("urn:altinn:authlevel", "3"),
                new Claim("client_amr", "virksomhetssertifikat"),
                new Claim("pid", "11015699332"),
                new Claim("token_type", "Bearer"),
                new Claim("client_id", "5b7b5418-1196-4539-bd1b-5f7c6fdf5963"),
                new Claim("http://schemas.microsoft.com/claims/authnclassreference", "Level3"),
                new Claim("exp", "1721895043"),
                new Claim("iat", "1721893243"),
                new Claim("client_orgno", "991825827"),
                new Claim("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:991825827\"}"),
                new Claim("iss", "https://platform.tt02.altinn.no/authentication/api/v1/openid/"),
                new Claim("actual_iss", "mock"),
                new Claim("nbf", "1721893243"),
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "1"),
                new Claim("urn:altinn:userid", "1"),
                new Claim("urn:altinn:partyid", "1")
        };

        public MockPolicyEvaluator(List<Claim> customClaims)
        {
            _customClaims = customClaims;
        }

        public virtual async Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
        {
            var principal = new ClaimsPrincipal();
            // Combine default and custom claims
            var allClaims = _defaultClaims.Concat(_customClaims).ToList();
            principal.AddIdentity(new ClaimsIdentity(allClaims, "MockScheme"));
            // Check if the user meets the authorization policy's requirements
            foreach (var requirement in policy.Requirements.OfType<ScopeAccessRequirement>())
            {
                bool hasMatchingClaim = allClaims
                    .Any(claim => requirement.Scope
                        .Any(scope => scope.Equals(claim.Value)));

                if (!hasMatchingClaim)
                {
                    return await Task.FromResult(AuthenticateResult.Fail($"Missing or invalid claim: {requirement.Scope}"));
                }
            }
            return await Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,
                new AuthenticationProperties(), "MockScheme")));
        }

        public virtual async Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy, AuthenticateResult authenticationResult, HttpContext context, object resource)
        {
            if (authenticationResult == null)
            {
                return PolicyAuthorizationResult.Forbid();
            }

            var user = authenticationResult.Principal;
            if (user == null)
            {
                return PolicyAuthorizationResult.Forbid();
            }

            foreach (var requirement in policy.Requirements.OfType<ScopeAccessRequirement>())
            {
                if (requirement.Scope.Any(scope => user.HasClaim("scope", scope)))
                {
                    return PolicyAuthorizationResult.Success();
                }
                return PolicyAuthorizationResult.Forbid();
            }
            return PolicyAuthorizationResult.Forbid();
        }
    }
}