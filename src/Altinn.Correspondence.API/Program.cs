using Altinn.Common.PEP.Authorization;
using Altinn.Correspondence.API.Helpers;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations;
using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Persistence;
using Azure.Identity;
using Hangfire;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text.Json.Serialization;

BuildAndRun(args);

static void BuildAndRun(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true);
    ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseCors(AuthorizationConstants.ArbeidsflateCors);
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var _Db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (_Db != null)
        {
            if (_Db.Database.GetPendingMigrations().Any())
            {
                _Db.Database.Migrate();
            }
        }
        app.UseHangfireDashboard();
    }


    app.Run();
}

static void ConfigureServices(IServiceCollection services, IConfiguration config, IHostEnvironment hostEnvironment)
{
    var connectionString = GetConnectionString(config);

    services.AddHostedService<EdDsaSecurityKeysCacheService>();
    services.Configure<AttachmentStorageOptions>(config.GetSection(key: nameof(AttachmentStorageOptions)));
    services.Configure<AltinnOptions>(config.GetSection(key: nameof(AltinnOptions)));
    services.Configure<DialogportenSettings>(config.GetSection(key: nameof(DialogportenSettings)));

    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    var altinnOptions = new AltinnOptions();
    config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
    services.AddCors(options =>
    {
        options.AddPolicy(name: AuthorizationConstants.ArbeidsflateCors,
                          policy =>
                          {
                              policy.WithOrigins("https://af.tt.altinn.no").SetIsOriginAllowedToAllowWildcardSubdomains();
                              policy.WithMethods("GET", "POST");
                              policy.WithHeaders("Authorization");
                              policy.AllowCredentials();
                          });
    });
    services.AddAuthentication()
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
            options.MetadataAddress = "https://platform.tt02.altinn.no/dialogporten/api/v1/.well-known/oauth-authorization-server";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKeyResolver = (_, _, _, _) => EdDsaSecurityKeysCacheService.EdDsaSecurityKeys,
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = !hostEnvironment.IsDevelopment(),
                ClockSkew = TimeSpan.Zero
            };
        });
    services.AddTransient<IAuthorizationHandler, ScopeAccessHandler>();
    services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationConstants.Sender, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.SenderScope)).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));
        options.AddPolicy(AuthorizationConstants.Recipient, policy =>
            policy.RequireScopesUnlessDialogporten(config, AuthorizationConstants.RecipientScope).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.DialogportenScheme));
        options.AddPolicy(AuthorizationConstants.SenderOrRecipient, policy =>
            policy.RequireScopesUnlessDialogporten(config, AuthorizationConstants.SenderScope, AuthorizationConstants.RecipientScope).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.DialogportenScheme));
        options.AddPolicy(AuthorizationConstants.DialogportenPolicy, policy =>
        {
            policy.AddAuthenticationSchemes(AuthorizationConstants.DialogportenScheme).RequireAuthenticatedUser();
        });
        options.AddPolicy(AuthorizationConstants.Migrate, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.MigrateScope)).AddAuthenticationSchemes(AuthorizationConstants.MaskinportenScheme));
        options.AddPolicy(AuthorizationConstants.NotificationCheck, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.NotificationCheckScope)).AddAuthenticationSchemes(AuthorizationConstants.MaskinportenScheme));
        options.AddPolicy("DownloadAttachment", policy =>
        {
            policy.RequireScopesUnlessDialogporten(config, AuthorizationConstants.SenderScope, AuthorizationConstants.RecipientScope).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.DialogportenScheme);
        });
    });
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();
    services.AddApplicationInsightsTelemetry();

    services.AddApplicationHandlers();
    services.AddPersistence(config);
    services.AddIntegrations(config, hostEnvironment);

    services.AddHttpClient();
    services.AddProblemDetails();

    services.ConfigureHangfire();

    services.Configure<KestrelServerOptions>(options =>
    {
        options.Limits.MaxRequestBodySize = null;
    });
    services.Configure<FormOptions>(options =>
    {
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartBodyLengthLimit = long.MaxValue;
        options.MultipartHeadersLengthLimit = int.MaxValue;
    });
}

static string GetConnectionString(IConfiguration config)
{
    var connectionString = config.GetSection("DatabaseOptions:ConnectionString").Value ?? Environment.GetEnvironmentVariable("DatabaseOptions__ConnectionString");
    if (connectionString == null)
    {
        throw new ArgumentNullException("DatabaseOptions__ConnectionString");
    }
    if (string.IsNullOrWhiteSpace(new NpgsqlConnectionStringBuilder(connectionString).Password))
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
        var token = credential
            .GetToken(
                new Azure.Core.TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" })
            );
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        connectionStringBuilder.Password = token.Token;
        return connectionStringBuilder.ToString();

    }
    return connectionString;
}
public partial class Program { }
