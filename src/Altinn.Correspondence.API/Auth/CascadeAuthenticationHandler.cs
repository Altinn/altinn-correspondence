using Altinn.Correspondence.API.Auth;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Web; 
using Microsoft.Extensions.Caching.Hybrid;

public class CascadeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>, IAuthenticationSignInHandler
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    //private readonly IDistributedCache _cache;
    private readonly GeneralSettings _generalSettings;
    private readonly IdportenTokenValidator _tokenValidator;
    private readonly HybridCache _cache;

    public CascadeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IAuthenticationSchemeProvider schemeProvider,
        IHttpContextAccessor httpContextAccessor,
        IOptions<GeneralSettings> generalSettings,
        IdportenTokenValidator tokenValidator,
        HybridCache cache)  // Change this from IDistributedCache to HybridCache
        : base(options, logger, encoder, clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _generalSettings = generalSettings.Value;
        _schemeProvider = schemeProvider;
        _tokenValidator = tokenValidator;
        _cache = cache;
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
            Logger.LogInformation($"Attempting authentication with scheme: {schemeName}");
            var scheme = await _schemeProvider.GetSchemeAsync(schemeName);
            if (scheme == null)
            {
                Logger.LogWarning($"Scheme {schemeName} is not registered.");
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
                Logger.LogInformation($"Authentication succeeded with scheme: {schemeName}");
                return result;
            }
        }

        Logger.LogInformation("All authentication schemes failed. Challenge.");
        return AuthenticateResult.NoResult();
    }

    private async Task<AuthenticateResult> HandleOpenIdConnectAsync()
    {
        if (!Request.Query.TryGetValue("session", out var sessionId))
        {
            return AuthenticateResult.NoResult();
        }

        var token = await _cache.GetOrCreateAsync(sessionId, async entry =>
        {
            var cacheEntryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
            return await Task.FromResult<string>(null);
        });
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }
        _cache.RemoveAsync((string)sessionId, CancellationToken.None);

        var principal = await _tokenValidator.ValidateTokenAsync(token);
        if (principal is not null)
        {
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        return AuthenticateResult.Fail(new SecurityTokenMalformedException("Could not validate ID-Porten token"));

    }

    public async Task SignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {

        // var token = await _cache.GetOrCreateAsync(sessionId, async entry =>
        // {
        //     var cacheEntryOptions = new DistributedCacheEntryOptions
        //     {
        //         AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        //     };
        //     return await Task.FromResult<string>(null);
        // });

        var sessionId = Guid.NewGuid().ToString();        
await _cache.GetOrCreateAsync(
    sessionId,
    async token =>
    {
        var cacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        return properties.Items[".Token.access_token"] 
            ?? throw new SecurityTokenMalformedException("Token should have contained an access token");
    });

        var redirectUrl = properties?.Items["endpoint"] ?? throw new SecurityTokenMalformedException("Should have had an endpoint");
        redirectUrl = AppendSessionToUrl($"{_generalSettings.CorrespondenceBaseUrl.TrimEnd('/')}{redirectUrl}", sessionId);
        Response.Redirect(redirectUrl);
    }

    public Task SignOutAsync(AuthenticationProperties? properties)
    {
        if (Request.Query.TryGetValue("session", out var sessionId))
        {
            _cache.RemoveAsync((string)sessionId, CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var redirectUrl = _httpContextAccessor.HttpContext.Request.Path;
        properties.RedirectUri = redirectUrl;
        properties.Items.Add("endpoint", redirectUrl);
        if(_httpContextAccessor.HttpContext.Request.Headers["Authorization"].ToString().StartsWith("Bearer")) 
        {
            return Context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme, properties);
        }
        else
        {
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

