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
        public MockPolicyEvaluator()
        {
        }
        public virtual async Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
        {
            // Preserve the claims from the test context
            var claims = context.User.Claims;
            var principal = new ClaimsPrincipal();
            
            // Determine the authentication scheme based on claims
            var issuer = claims.FirstOrDefault(c => c.Type == "iss")?.Value;
            var authenticationScheme = "Bearer"; // Default to JWT Bearer for Altinn tokens
            
            if (issuer != null && issuer.Contains("dialogporten"))
            {
                authenticationScheme = AuthorizationConstants.DialogportenScheme;
            } 
            else if (issuer != null && issuer.Contains("maskinporten"))
            {
                authenticationScheme = AuthorizationConstants.MaskinportenScheme;
            }
            
            principal.AddIdentity(new ClaimsIdentity(claims, authenticationScheme));
            return AuthenticateResult.Success(new AuthenticationTicket(principal, authenticationScheme));
        }

        public virtual async Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy, AuthenticateResult authenticationResult, HttpContext context, object? resource)
        {
            var user = authenticationResult.Principal;
            
            // Handle custom assertion requirements (e.g., the new Sender policy)
            foreach (var requirement in policy.Requirements.OfType<AssertionRequirement>())
            {
                var authzContext = new AuthorizationHandlerContext(new[] { requirement }, user, resource);
                var result = await Task.Run(() => requirement.Handler(authzContext));
                if (!result)
                {
                    return PolicyAuthorizationResult.Forbid();
                }
            }

            // Handle other standard requirements like DenyAnonymousAuthorizationRequirement
            foreach (var requirement in policy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>())
            {
                if (!user?.Identity?.IsAuthenticated == true)
                {
                    return PolicyAuthorizationResult.Forbid();
                }
            }

            // Handle role-based requirements
            foreach (var requirement in policy.Requirements.OfType<RolesAuthorizationRequirement>())
            {
                var hasRole = requirement.AllowedRoles.Any(role => user?.IsInRole(role) == true);
                if (!hasRole)
                {
                    return PolicyAuthorizationResult.Forbid();
                }
            }

            return PolicyAuthorizationResult.Success();
        }
    }
}
    