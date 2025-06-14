using Altinn.Common.PEP.Authorization;
using Altinn.Correspondence.API.Helpers;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Correspondence.API.Auth
{
    public static class DependencyInjection
    {
        public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration config, IHostEnvironment hostEnvironment)
        {
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            var idPortenSettings = new IdportenSettings();
            config.GetSection(nameof(IdportenSettings)).Bind(idPortenSettings);
            var dialogportenSettings = new DialogportenSettings();
            config.GetSection(nameof(DialogportenSettings)).Bind(dialogportenSettings);
            var generalSettings = new GeneralSettings();
            config.GetSection(nameof(GeneralSettings)).Bind(generalSettings);
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = generalSettings.RedisConnectionString;
                options.InstanceName = "redisCache";
            });
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
                    options.Authority = idPortenSettings.Issuer;
                    options.ClientId = idPortenSettings.ClientId;
                    options.ClientSecret = idPortenSettings.ClientSecret;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.CallbackPath = "/correspondence/api/v1/idporten-callback";
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    
                    // Add distributed state management
                    //var cache = services.BuildServiceProvider().GetRequiredService<IDistributedCache>();
                    //options.StateDataFormat = new DistributedCacheStateDataFormat(cache, "OpenIdConnectState");
                    
                    options.Events = new OpenIdConnectEvents
                    {
                        OnRedirectToIdentityProvider = context =>
                        {
                            context.ProtocolMessage.RedirectUri = $"{generalSettings.CorrespondenceBaseUrl.TrimEnd('/')}{options.CallbackPath}";
                            //Console.WriteLine($"Redirecting to identity provider: {context.ProtocolMessage.RedirectUri}");
                            //context.ProtocolMessage.LoginHint = "testid:12345678901_idporten-loa-high";
                            //context.ProtocolMessage.Scope = "openid profile";
                            return Task.CompletedTask;
                        },
                        OnMessageReceived = context =>
                        {
                            Console.WriteLine($"Message received from identity provider. Code: {context.ProtocolMessage.Code}, State: {context.ProtocolMessage.State}");
                            
                            // This is to handle the case where the user is redirected back to the application with an error
                            if (context.ProtocolMessage.Error is not null)
                            {
                                Console.WriteLine($"Error received from identity provider: {context.ProtocolMessage.Error}");
                            }
                            return Task.CompletedTask;
                        },
                        OnRemoteFailure = context =>
                        {
                            Console.WriteLine($"Remote failure: {context.Failure}");
                            if (context.Failure is Exception ex)
                            {
                                Console.WriteLine($"Exception details: {ex}");
                            }
                            return Task.CompletedTask;
                        },
                        OnTokenResponseReceived = context =>
                        {
                            // This is to handle the case where the token response is received
                            if (context.ProtocolMessage.AccessToken is not null)
                            {
                                Console.WriteLine($"Access token received: {context.ProtocolMessage.AccessToken}");
                            }
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = async context =>
                        {
                            Console.WriteLine("Token validated");
                            var sessionId = Guid.NewGuid().ToString();
                            Console.WriteLine($"SessionId: {sessionId}");
                            var cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                            
                            if (context.TokenEndpointResponse?.AccessToken == null)
                            {
                                Console.WriteLine("No access token received in TokenEndpointResponse");
                                return;
                            }
                            
                            Console.WriteLine($"Storing token in cache for session {sessionId}");
                            await cache.SetStringAsync(
                                sessionId, 
                                context.TokenEndpointResponse.AccessToken,
                                new DistributedCacheEntryOptions
                                {
                                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                                });
                            Console.WriteLine($"Successfully stored token in cache for session {sessionId}");
                            
                            var redirectUrl = context.Properties?.Items["endpoint"] ?? throw new SecurityTokenMalformedException("Should have had an endpoint");
                            redirectUrl = CascadeAuthenticationHandler.AppendSessionToUrl($"{generalSettings.CorrespondenceBaseUrl.TrimEnd('/')}{redirectUrl}", sessionId);
                            Console.WriteLine($"Redirecting to {redirectUrl} with session {sessionId}");
                            context.Properties.RedirectUri = redirectUrl;
                        }
                    };
                });
        }

        public static void ConfigureAuthorization(this IServiceCollection services, IConfiguration config)
        {
            services.AddTransient<IAuthorizationHandler, ScopeAccessHandler>();
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationConstants.Sender, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.SenderScope)).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));
                options.AddPolicy(AuthorizationConstants.Recipient, policy =>
                    policy.RequireScopeIfAltinn(config, AuthorizationConstants.RecipientScope).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.DialogportenScheme));
                options.AddPolicy(AuthorizationConstants.SenderOrRecipient, policy =>
                    policy.RequireScopeIfAltinn(config, AuthorizationConstants.SenderScope, AuthorizationConstants.RecipientScope).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.DialogportenScheme));
                options.AddPolicy(AuthorizationConstants.DialogportenPolicy, policy =>
                {
                    policy.AddAuthenticationSchemes(AuthorizationConstants.DialogportenScheme).RequireAuthenticatedUser();
                });
                options.AddPolicy(AuthorizationConstants.Migrate, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.MigrateScope)).AddAuthenticationSchemes(AuthorizationConstants.MaskinportenScheme));
                options.AddPolicy(AuthorizationConstants.NotificationCheck, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.NotificationCheckScope)).AddAuthenticationSchemes(AuthorizationConstants.MaskinportenScheme));
                options.AddPolicy(AuthorizationConstants.DownloadAttachmentPolicy, policy =>
                    policy.RequireScopeIfAltinn(config, AuthorizationConstants.RecipientScope)
                          .AddAuthenticationSchemes(AuthorizationConstants.AllSchemes));
                options.AddPolicy(AuthorizationConstants.Legacy, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.LegacyScope)).AddAuthenticationSchemes(AuthorizationConstants.LegacyScheme));
            });
        }
    }
}
