using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.Helpers
{

    internal class MockPolicyEvaluator : IPolicyEvaluator
    {
        public virtual async Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
        {
            var principal = new ClaimsPrincipal();

            principal.AddIdentity(new ClaimsIdentity(new[]
            {
                new Claim("urn:altinn:authlevel", "3"),
                new Claim("client_amr", "virksomhetssertifikat"),
                new Claim("pid", "11015699332"),
                new Claim("token_type", "Bearer"),
                new Claim("client_id", "5b7b5418-1196-4539-bd1b-5f7c6fdf5963"),
                new Claim("http://schemas.microsoft.com/claims/authnclassreference", "Level3"),
                new Claim("scope", "altinn:correspondence"),
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
            }, "MockSheme"));
            return await Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,
             new AuthenticationProperties(), "FakeScheme")));
        }

        public virtual async Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy,
         AuthenticateResult authenticationResult, HttpContext context, object resource)
        {
            return await Task.FromResult(PolicyAuthorizationResult.Success());
        }
    }
}