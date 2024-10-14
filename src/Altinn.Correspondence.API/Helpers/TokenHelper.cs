using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Authorization;

namespace Altinn.Correspondence.API.Helpers
{
    public static class TokenHelper
    {
        public static AuthorizationPolicyBuilder RequireScopeIfAltinn(this AuthorizationPolicyBuilder authorizationPolicyBuilder, IConfiguration config, params string[] scopes) => authorizationPolicyBuilder.RequireAssertion(context =>
        {
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            bool isAltinnToken = context.User.HasClaim(c => c.Issuer == $"{altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/openid/");
            if (isAltinnToken)
            {
                return context.User.HasClaim(c => c.Type == "scope" && scopes.Contains(c.Value));
            }
            return true;            
        });
    }

}
