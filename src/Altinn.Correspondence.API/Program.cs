using Altinn.Correspondence.API.Auth;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations;
using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Persistence;
using Azure.Identity;
using Altinn.Correspondence.Helpers;
using Hangfire;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json.Serialization;
using Altinn.Common.AccessToken.Configuration;

BuildAndRun(args);

static void BuildAndRun(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", true, true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true)
        .AddJsonFile("appsettings.local.json", true, true);
    ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

    var app = builder.Build();

    app.UseExceptionHandler();

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
    services.Configure<IdportenSettings>(config.GetSection(key: nameof(IdportenSettings)));
    services.Configure<GeneralSettings>(config.GetSection(key: nameof(GeneralSettings)));
    services.Configure<KeyVaultSettings>(config.GetSection(key: nameof(KeyVaultSettings)));

    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    var altinnOptions = new AltinnOptions();
    config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
    services.AddExceptionHandler<SlackExceptionNotification>();
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
    services.ConfigureAuthentication(config, hostEnvironment);
    services.ConfigureAuthorization(config);
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();
    services.AddApplicationInsightsTelemetry();

    services.AddApplicationHandlers();
    services.AddPersistence(config);
    services.AddIntegrations(config);

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
