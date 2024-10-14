using Altinn.Common.PEP.Authorization;
using Altinn.Correspondence.API.Helpers;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
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
                        OnAuthenticationFailed = context => JWTBearerEventsHelper.OnAuthenticationFailed(context)
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
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = "CorrespondenceIdportenSession";
                    options.Cookie.SameSite = SameSiteMode.None;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.IsEssential = true;
                    options.ExpireTimeSpan = TimeSpan.FromSeconds(10); // Must be transient/short-lived
                    options.SlidingExpiration = false;
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.NonceCookie.SameSite = SameSiteMode.None;
                    options.CorrelationCookie.SameSite = SameSiteMode.None;
                    options.AuthenticationMethod = OpenIdConnectRedirectBehavior.RedirectGet;
                    options.ResponseMode = OpenIdConnectResponseMode.FormPost;
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
                    options.Events = new OpenIdConnectEvents
                    {
                        OnRedirectToIdentityProvider = context =>
                        {
                            context.ProtocolMessage.RedirectUri = $"{dialogportenSettings.CorrespondenceBaseUrl.TrimEnd('/')}{options.CallbackPath}";
                            return Task.CompletedTask;
                        }
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, CascadeAuthenticationHandler>(AuthorizationConstants.AllSchemes, options => { });
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
                });
        }
    }
}
