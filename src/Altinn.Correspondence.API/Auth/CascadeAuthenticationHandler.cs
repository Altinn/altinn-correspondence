using Altinn.Correspondence.API.Auth;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Web;

public class CascadeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>, IAuthenticationSignInHandler
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHybridCacheWrapper _cache;
    private readonly GeneralSettings _generalSettings;
    private readonly IdportenTokenValidator _tokenValidator;
    private readonly ILogger<CascadeAuthenticationHandler> _logger;

    public CascadeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ISystemClock clock,
        IAuthenticationSchemeProvider schemeProvider,
        IHttpContextAccessor httpContextAccessor,
        IOptions<GeneralSettings> generalSettings,
        IdportenTokenValidator tokenValidator,
        IHybridCacheWrapper cache)
        : base(options, loggerFactory, encoder, clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _generalSettings = generalSettings.Value;
        _schemeProvider = schemeProvider;
        _tokenValidator = tokenValidator;
        _cache = cache;
        _logger = loggerFactory.CreateLogger<CascadeAuthenticationHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var schemesToTry = new[]
        {
            JwtBearerDefaults.AuthenticationScheme,
            AuthorizationConstants.DialogportenScheme,
            OpenIdConnectDefaults.AuthenticationScheme
        };

        foreach (var schemeName in schemesToTry)
        {
            _logger.LogInformation($"Attempting authentication with scheme: {schemeName}");
            var scheme = await _schemeProvider.GetSchemeAsync(schemeName);
            if (scheme == null)
            {
                _logger.LogWarning($"Scheme {schemeName} is not registered.");
                continue;
            }
            AuthenticateResult? result;
            if (schemeName == OpenIdConnectDefaults.AuthenticationScheme)
            {
                result = await HandleOpenIdConnectAsync();
            }
            else
            {
                result = await Context.AuthenticateAsync(schemeName);
            }
            if (result.Succeeded)
            {
                _logger.LogInformation($"Authentication succeeded with scheme: {schemeName}");
                return result;
            }
        }

        _logger.LogInformation("All authentication schemes failed. Challenge.");
        return AuthenticateResult.NoResult();
    }

    private async Task<AuthenticateResult> HandleOpenIdConnectAsync()
    {
        if (!Request.Query.TryGetValue("session", out var sessionId))
        {
            _logger.LogInformation("No session ID found in query parameters");
            return AuthenticateResult.NoResult();
        }

        _logger.LogInformation("Attempting to retrieve token for session {SessionId}", sessionId);
        var token = await _cache.GetAsync<string>(sessionId);
        
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No token found in cache for session {SessionId}", sessionId);
            return AuthenticateResult.NoResult();
        }
        _logger.LogInformation("Successfully retrieved token from cache for session {SessionId}", sessionId);
        await _cache.RemoveAsync(sessionId);
        _logger.LogDebug("Removed token from cache for session {SessionId}", sessionId);

        try 
        {
            var principal = await _tokenValidator.ValidateTokenAsync(token);
            if (principal is not null)
            {
                _logger.LogInformation("Successfully validated token for session {SessionId}", sessionId);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }
            _logger.LogWarning("Failed to validate token for session {SessionId}", sessionId);
            return AuthenticateResult.Fail(new SecurityTokenMalformedException("Could not validate ID-Porten token"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token for session {SessionId}", sessionId);
            return AuthenticateResult.Fail(ex);
        }
    }

    public async Task SignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        var sessionId = Guid.NewGuid().ToString();
        _logger.LogInformation("Storing token in cache for session {SessionId} in SignInAsync", sessionId);
        
        await _cache.SetAsync(
            sessionId,
            properties.Items[".Token.access_token"],
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5)
            });
        _logger.LogInformation("Successfully stored token in cache for session {SessionId} in SignInAsync", sessionId);
        
        var redirectUrl = properties?.Items["endpoint"] ?? throw new SecurityTokenMalformedException("Should have had an endpoint");
        redirectUrl = AppendSessionToUrl($"{_generalSettings.CorrespondenceBaseUrl.TrimEnd('/')}{redirectUrl}", sessionId);
        _logger.LogInformation("Redirecting to {RedirectUrl} with session {SessionId}", redirectUrl, sessionId);
        Response.Redirect(redirectUrl);
    }

    public async Task SignOutAsync(AuthenticationProperties? properties)
    {
        if (Request.Query.TryGetValue("session", out var sessionId))
        {
            await _cache.RemoveAsync(sessionId);
        }
        await Task.CompletedTask;
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var redirectUrl = _httpContextAccessor.HttpContext.Request.Path;
        properties.RedirectUri = redirectUrl;
        properties.Items.Add("endpoint", redirectUrl);
        if(_httpContextAccessor.HttpContext.Request.Headers["Authorization"].ToString().StartsWith("Bearer")) 
        {
            _logger.LogInformation("Challenging with JwtBearer scheme for endpoint {Endpoint}", redirectUrl);
            return Context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme, properties);
        }
        else
        {
            _logger.LogInformation("Challenging with OpenIdConnect scheme for endpoint {Endpoint}", redirectUrl);
            return Context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
        }   
    }

    public static string AppendSessionToUrl(string url, string sessionId)
    {
        var uriBuilder = new UriBuilder(url);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["session"] = sessionId;
        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }
}

