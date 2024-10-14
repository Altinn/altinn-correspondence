using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace Altinn.Correspondence.API.Helpers
{
    public static class TokenHelper
    {
        public static AuthorizationPolicyBuilder RequireScopesUnlessDialogporten(this AuthorizationPolicyBuilder authorizationPolicyBuilder, IConfiguration config, params string[] scopes) => authorizationPolicyBuilder.RequireAssertion(context =>
        {
            var dialogportenSettings = new DialogportenSettings();
            config.GetSection(nameof(DialogportenSettings)).Bind(dialogportenSettings);
            bool isDialogportenToken = context.User.HasClaim(c => c.Issuer == dialogportenSettings.Issuer);
            if (isDialogportenToken)
            {
                return true; // Allow Dialogporten without checking scopes
            }
            return context.User.HasClaim(c => c.Type == "scope" && scopes.Contains(c.Value));
        });
    }
}
