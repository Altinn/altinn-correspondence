using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Altinn.Correspondence.API.Auth;
public class IdportenTokenValidator
{
    private readonly IOptionsMonitor<OpenIdConnectOptions> _oidcOptions;

    public IdportenTokenValidator(IOptionsMonitor<OpenIdConnectOptions> oidcOptions)
    {
        _oidcOptions = oidcOptions;
    }

    public async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
    {
        var options = _oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);

        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            options.Configuration?.AuthorizationEndpoint ?? options.Authority + "/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());

        var config = await configManager.GetConfigurationAsync(CancellationToken.None);
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = options.TokenValidationParameters.ValidateIssuer,
            ValidIssuer = config.Issuer,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = options.TokenValidationParameters.ValidateLifetime
        };

        try
        {
            var claimsPrincipal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            if (!(validatedToken is JwtSecurityToken jwtSecurityToken) ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.RsaSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return claimsPrincipal;
        }
        catch (Exception)
        {
            return null; // Token validation failed
        }
    }
}