using Altinn.Correspondence.Application.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

public class CascadeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;

    public CascadeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IAuthenticationSchemeProvider schemeProvider)
        : base(options, logger, encoder, clock)
    {
        _schemeProvider = schemeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Define the order of schemes to try
        var schemesToTry = new[]
        {
            JwtBearerDefaults.AuthenticationScheme,
            AuthorizationConstants.DialogportenScheme,
            OpenIdConnectDefaults.AuthenticationScheme
        };

        foreach (var schemeName in schemesToTry)
        {
            Logger.LogInformation($"Attempting authentication with scheme: {schemeName}");

            var scheme = await _schemeProvider.GetSchemeAsync(schemeName);
            if (scheme == null)
            {
                Logger.LogWarning($"Scheme {schemeName} is not registered.");
                continue;
            }

            var result = await Context.AuthenticateAsync(schemeName);

            if (result.Succeeded)
            {
                Logger.LogInformation($"Authentication succeeded with scheme: {schemeName}");
                return result;
            }
            else
            {
                Logger.LogInformation($"Authentication failed with scheme: {schemeName}. Reason: {result.Failure?.Message}");
            }

            // If it's OpenIdConnect and it failed, we don't want to redirect yet
            if (schemeName == OpenIdConnectDefaults.AuthenticationScheme)
            {
                return AuthenticateResult.NoResult();
            }
        }

        Logger.LogInformation("All authentication schemes failed. Returning NoResult.");
        return AuthenticateResult.NoResult();
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Redirect to OpenIdConnect login only if all other schemes have failed
        return Context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
    }
}