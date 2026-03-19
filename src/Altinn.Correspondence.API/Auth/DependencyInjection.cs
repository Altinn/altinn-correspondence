using Altinn.Common.PEP.Authorization;
using Altinn.Correspondence.API.Helpers;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Correspondence.API.Auth
{
    public static class DependencyInjection
    {
        private static IHybridCacheWrapper? _cache;

        public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration config, IHostEnvironment hostEnvironment)
        {
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            var altinnIdProviderSettings = new AltinnIdProviderSettings();
            config.GetSection(nameof(AltinnIdProviderSettings)).Bind(altinnIdProviderSettings);
            var dialogportenSettings = new DialogportenSettings();
            config.GetSection(nameof(DialogportenSettings)).Bind(dialogportenSettings);
            var generalSettings = new GeneralSettings();
            config.GetSection(nameof(GeneralSettings)).Bind(generalSettings);
            _cache = services.BuildServiceProvider().GetRequiredService<IHybridCacheWrapper>();
            services.AddTransient<IdportenTokenValidator>();
            services
                .AddAuthentication()
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.SaveToken = true;
                    options.MetadataAddress = altinnOptions.OpenIdWellKnown;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = false,
                        RequireExpirationTime = true,
                        ValidateLifetime = !hostEnvironment.IsDevelopment(), // Do not validate lifetime in tests
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Events = new JwtBearerEvents()
                    {
                        OnAuthenticationFailed = AltinnTokenEventsHelper.OnAuthenticationFailed,
                        OnChallenge = AltinnTokenEventsHelper.OnChallenge
                    };
                })
                .AddJwtBearer(AuthorizationConstants.LegacyScheme, options =>
                {
                    options.SaveToken = true;
                    options.MetadataAddress = altinnOptions.OpenIdWellKnown;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        RequireExpirationTime = true,
                        ValidateLifetime = !hostEnvironment.IsDevelopment(), // Do not validate lifetime in tests
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Events = new JwtBearerEvents()
                    {
                        OnAuthenticationFailed = AltinnTokenEventsHelper.OnAuthenticationFailed,
                        OnChallenge = AltinnTokenEventsHelper.OnChallenge
                    };
                })
                .AddJwtBearer(AuthorizationConstants.MaskinportenScheme, options => // To support maskinporten tokens 
                {
                    options.SaveToken = true;
                    if (hostEnvironment.IsProduction())
                    {
                        options.MetadataAddress = "https://maskinporten.no/.well-known/oauth-authorization-server";
                    }
                    else
                    {
                        options.MetadataAddress = "https://test.maskinporten.no/.well-known/oauth-authorization-server";
                    }
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = false,
                        RequireExpirationTime = true,
                        ValidateLifetime = !hostEnvironment.IsDevelopment(),
                        ClockSkew = TimeSpan.Zero
                    };
                })
                .AddJwtBearer(AuthorizationConstants.DialogportenScheme, options =>
                {
                    options.SaveToken = true;
                    options.MetadataAddress = $"{dialogportenSettings.Issuer}/.well-known/jwks.json";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        IssuerSigningKeyResolver = (_, _, _, _) => EdDsaSecurityKeysCacheService.EdDsaSecurityKeys,
                        ValidIssuer = dialogportenSettings.Issuer,
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = false,
                        RequireExpirationTime = true,
                        ValidateLifetime = !hostEnvironment.IsDevelopment(),
                        ClockSkew = TimeSpan.Zero
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, CascadeAuthenticationHandler>(AuthorizationConstants.AllSchemes, options => { })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.SignInScheme = AuthorizationConstants.AllSchemes;
                    options.ResponseMode = OpenIdConnectResponseMode.Query;
                    options.Authority = $"{altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/openid";
                    options.ClientId = altinnIdProviderSettings.ClientId;
                    options.ClientSecret = altinnIdProviderSettings.ClientSecret;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.CallbackPath = "/correspondence/api/v1/idporten-callback";
                    options.SaveTokens = true;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.CorrelationCookie.SameSite = SameSiteMode.None;
                    options.CorrelationCookie.HttpOnly = true;
                    options.CorrelationCookie.Name = ".AspNetCore.Correlation.Correspondence";
                    options.CorrelationCookie.Path = options.CallbackPath.Value;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.StateDataFormat = new DistributedCacheStateDataFormat(_cache, "OpenIdConnectState");
                    options.SkipUnrecognizedRequests = true;
                    
                    static void RestartLogin(
                        RemoteFailureContext context,
                        ILogger logger,
                        GeneralSettings generalSettings)
                    {
                        // Try to get original endpoint (contains correspondenceId/attachmentId)
                        string? endpoint = null;
                        if (context.Properties?.Items != null &&
                            context.Properties.Items.TryGetValue("endpoint", out var ep) &&
                            !string.IsNullOrEmpty(ep))
                        {
                            endpoint = ep;
                        }

                        // Avoid infinite loops: only attempt one restart
                        const string retryKey = "oidcRetry";
                        const string retryMarker = "oidcRetry=1";

                        var alreadyRetried =
                            context.Request.Query.ContainsKey(retryKey) ||
                            (context.Request.QueryString.HasValue &&
                             context.Request.QueryString.Value!.Contains(retryMarker, StringComparison.OrdinalIgnoreCase));

                        static void ExpireCookie(HttpResponse response, string name, string path)
                        {
                            response.Cookies.Append(
                                name,
                                string.Empty,
                                new CookieOptions
                                {
                                    Expires = DateTimeOffset.UnixEpoch,
                                    Path = path,
                                    Secure = true,
                                    SameSite = SameSiteMode.None,
                                    HttpOnly = true
                                });
                        }

                        // Clear correlation + nonce cookies best-effort (helps avoid "stuck" retries)
                        var correlationPrefix = context.Options.CorrelationCookie.Name;
                        // Nonce cookie isn't always exposed on context.Options; use well-known prefix.
                        const string noncePrefix = ".AspNetCore.OpenIdConnect.Nonce.";
                        var callbackPath = context.Options.CallbackPath.Value ?? "/correspondence/api/v1/idporten-callback";

                        foreach (var cookieName in context.Request.Cookies.Keys)
                        {
                            if (cookieName.StartsWith(correlationPrefix, StringComparison.OrdinalIgnoreCase) ||
                                cookieName.StartsWith(noncePrefix, StringComparison.OrdinalIgnoreCase))
                            {
                                ExpireCookie(context.Response, cookieName, "/");
                                ExpireCookie(context.Response, cookieName, callbackPath);
                            }
                        }

                        // Where to restart the flow
                        string restartUrl;
                        if (alreadyRetried)
                        {
                            restartUrl = generalSettings.ArbeidsflateBaseUrl;
                        }
                        else if (!string.IsNullOrEmpty(endpoint))
                        {
                            var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
                            restartUrl = $"{generalSettings.CorrespondenceBaseUrl.TrimEnd('/')}{endpoint}{separator}{retryMarker}";
                        }
                        else
                        {
                            restartUrl = generalSettings.ArbeidsflateBaseUrl;
                        }
                        logger.LogWarning(
                            "Restarting OIDC flow after failure ({FailureType}). " +
                            "Redirecting to '{RestartUrl}'. Endpoint='{Endpoint}', Host={Host}, Path={Path}, Query={Query}",
                            context.Failure?.GetType().Name ?? "<unknown>",
                            restartUrl,
                            endpoint ?? "<none>",
                            context.Request.Host.Value,
                            context.Request.Path.Value,
                            context.Request.QueryString.Value?.SanitizeForLogging());
                        context.Response.Redirect(restartUrl);
                        context.HandleResponse(); 
                    }
                    options.Events = new OpenIdConnectEvents
                    {
                        OnRedirectToIdentityProvider = context =>
                        {
                            context.ProtocolMessage.RedirectUri =
                                $"{generalSettings.CorrespondenceBaseUrl.TrimEnd('/')}{options.CallbackPath}";
                            context.ProtocolMessage.AcrValues = "selfregistered-email idporten-loa-substantial";
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = async context =>
                        {
                            var sessionId = Guid.NewGuid().ToString();
                            if (context.TokenEndpointResponse?.AccessToken == null)
                            {
                                return;
                            }
                            await _cache.SetAsync(
                                sessionId,
                                context.TokenEndpointResponse.AccessToken,
                                new HybridCacheEntryOptions
                                {
                                    Expiration = TimeSpan.FromMinutes(5)
                                });
                            var redirectUrl = context.Properties?.Items["endpoint"]
                                            ?? throw new SecurityTokenMalformedException("Should have had an endpoint");
                            redirectUrl = CascadeAuthenticationHandler.AppendSessionToUrl(
                                $"{generalSettings.CorrespondenceBaseUrl.TrimEnd('/')}{redirectUrl}",
                                sessionId);
                            context.Properties.RedirectUri = redirectUrl;
                        },
                        OnRemoteFailure = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("OpenIdConnectRemoteFailure");
                            logger.LogError(context.Failure,
                                "OIDC remote failure: {Message}",
                                context.Failure?.Message);
                            RestartLogin(context, logger, generalSettings);
                            return Task.CompletedTask;
                        },
                    };
                });
        }

        public static void ConfigureAuthorization(this IServiceCollection services, IConfiguration config)
        {
            services.AddTransient<IAuthorizationHandler, ScopeAccessHandler>();
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationConstants.Sender, policy =>
                    policy.RequireAssertion(SenderScopePolicy)
                          .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.MaskinportenScheme));
                options.AddPolicy(AuthorizationConstants.Recipient, policy =>
                    policy.RequireAssertion(RecipientScopePolicy)
                          .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.DialogportenScheme));
                options.AddPolicy(AuthorizationConstants.SenderOrRecipient, policy =>
                    policy.RequireAssertion(context => SenderScopePolicy(context) || RecipientScopePolicy(context))
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.MaskinportenScheme, AuthorizationConstants.DialogportenScheme));
                options.AddPolicy(AuthorizationConstants.DialogportenPolicy, policy =>
                {
                    policy.AddAuthenticationSchemes(AuthorizationConstants.DialogportenScheme).RequireAuthenticatedUser();
                });
                options.AddPolicy(AuthorizationConstants.Migrate, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.MigrateScope)).AddAuthenticationSchemes(AuthorizationConstants.MaskinportenScheme));
                options.AddPolicy(AuthorizationConstants.NotificationCheck, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.NotificationCheckScope)).AddAuthenticationSchemes(AuthorizationConstants.MaskinportenScheme));
                options.AddPolicy(AuthorizationConstants.DownloadAttachmentPolicy, policy =>
                    policy.RequireScopeIfAltinn(config, AuthorizationConstants.RecipientScope, AuthorizationConstants.LegacyScope)
                          .AddAuthenticationSchemes(AuthorizationConstants.AllSchemes));
                options.AddPolicy(AuthorizationConstants.Legacy, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.LegacyScope)).AddAuthenticationSchemes(AuthorizationConstants.LegacyScheme));
                options.AddPolicy(AuthorizationConstants.Maintenance, policy =>
                    policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.MaintenanceScope))
                          .AddAuthenticationSchemes(AuthorizationConstants.MaskinportenScheme));
            });
        }

        private static bool SenderScopePolicy(AuthorizationHandlerContext context)
        {
            var issuerClaim = context.User.Claims.FirstOrDefault(c => c.Type == "iss");
            if (issuerClaim == null) return false;

            // Altinn
            if (issuerClaim.Value.Contains("altinn"))
            {
                var scopeClaim = context.User.Claims.FirstOrDefault(c => c.Type == "scope");
                if (scopeClaim != null)
                {
                    var scopes = scopeClaim.Value.Split(' ');
                    if (scopes.Contains(AuthorizationConstants.MigrateScope))
                    {
                        return true;
                    }
                    return scopes.Contains(AuthorizationConstants.SenderScope);
                }
            }

            // Maskinporten
            if (issuerClaim.Value.Contains("maskinporten.no"))
            {
                var scopeClaim = context.User.Claims.FirstOrDefault(c => c.Type == "scope");
                if (scopeClaim != null)
                {
                    var scopes = scopeClaim.Value.Split(' ');
                    if (scopes.Contains(AuthorizationConstants.MigrateScope))
                    {
                        return true;
                    }
                    return scopes.Contains(AuthorizationConstants.ServiceOwnerScope) &&
                           scopes.Contains(AuthorizationConstants.SenderScope);
                }
            }

            return false;
        }

        private static bool RecipientScopePolicy(AuthorizationHandlerContext context)
        {
            var issuerClaim = context.User.Claims.FirstOrDefault(c => c.Type == "iss");
            if (issuerClaim == null) return false;

            // Dialogporten
            if (issuerClaim.Value.Contains("dialogporten"))
            {
                var actionsClaim = context.User.Claims.FirstOrDefault(c => c.Type == "a");
                if (actionsClaim != null)
                {
                    var actions = actionsClaim.Value.Split(';');
                    return actions.Contains("read");
                }
            }

            // Altinn
            if (issuerClaim.Value.Contains("altinn.no"))
            {
                var scopeClaim = context.User.Claims.FirstOrDefault(c => c.Type == "scope");
                if (scopeClaim != null)
                {
                    var scopes = scopeClaim.Value.Split(' ');
                    if (scopes.Contains(AuthorizationConstants.MigrateScope))
                    {
                        return true;
                    }
                    return scopes.Contains(AuthorizationConstants.RecipientScope);
                }
            }

            // Maskinporten
            if (issuerClaim.Value.Contains("maskinporten.no"))
            {
                var scopeClaim = context.User.Claims.FirstOrDefault(c => c.Type == "scope");
                if (scopeClaim != null)
                {
                    var scopes = scopeClaim.Value.Split(' ');
                    if (scopes.Contains(AuthorizationConstants.MigrateScope))
                    {
                        return true;
                    }
                    return scopes.Contains(AuthorizationConstants.RecipientScope);
                }
            }

            return false;
        }
    }
}
