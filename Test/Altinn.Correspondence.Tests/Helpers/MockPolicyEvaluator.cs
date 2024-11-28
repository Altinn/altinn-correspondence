using Altinn.Common.PEP.Authorization;
using Altinn.Correspondence.Common.Constants;
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
        private List<Claim> _claims;
        public MockPolicyEvaluator(List<Claim> customClaims)
        {
            _claims = customClaims;
        }
        public virtual async Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
        {
            var principal = new ClaimsPrincipal();
            // Check if the user meets the authorization policy's requirements
            foreach (var requirement in policy.Requirements.OfType<ScopeAccessRequirement>())
            {
                bool hasMatchingClaim = _claims
                    .Any(claim => requirement.Scope
                        .Any(scope => scope.Equals(claim.Value)));

                if (!hasMatchingClaim)
                {
                    return await Task.FromResult(AuthenticateResult.Fail($"Missing or invalid claim: {requirement.Scope}"));
                }
            }
            var issuer = _claims.FirstOrDefault(c => c.Type == "iss")?.Value;
            if (issuer.Contains("dialogporten"))
            {
                principal.AddIdentity(new ClaimsIdentity(_claims, AuthorizationConstants.DialogportenScheme));
            } else
            {
                principal.AddIdentity(new ClaimsIdentity(_claims, "MockScheme"));
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
            foreach (var requirement in policy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>())
            {
                if (user.Identity.IsAuthenticated)
                {
                    return PolicyAuthorizationResult.Success();
                }
                return PolicyAuthorizationResult.Forbid();
            }
            return PolicyAuthorizationResult.Forbid();
        }
    }
}
