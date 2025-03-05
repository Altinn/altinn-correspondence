using Altinn.Correspondence.API.Auth;
using Altinn.Correspondence.API.Helpers;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Helpers;
using Altinn.Correspondence.Integrations;
using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Azure.Identity;
using Hangfire;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Npgsql;
using Serilog;
using System.Reflection;
using System.Text.Json.Serialization;
using Altinn.Correspondence.Integrations.Slack;
using Microsoft.Extensions.Caching.Hybrid;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Integrations.Azure;
using Altinn.Correspondence.Application.IpSecurityRestrictionsUpdater;

BuildAndRun(args);

static void BuildAndRun(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithClientIp()
        .Enrich.With(new PropertyPropagationEnricher("correspondenceId", "instanceId", "resourceId", "partyId"))
        .WriteTo.Console()
        .WriteTo.ApplicationInsights(
            services.GetRequiredService<TelemetryConfiguration>(),
            TelemetryConverter.Traces));
    builder.Configuration
        .AddJsonFile("appsettings.json", true, true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true)
        .AddJsonFile("appsettings.local.json", true, true);
    ConfigureServices(builder.Services, builder.Configuration, builder.Environment);
    #pragma warning disable EXTEXP0018
    builder.Services.AddHybridCache();
    #pragma warning restore EXTEXP0018
    builder.Services.AddSingleton<IHybridCacheWrapper, HybridCacheWrapper>();

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
    app.UseMiddleware<AcceptHeaderValidationMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var _Db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (_Db != null)
        {
            _Db.MigrateWithLock();
        }
        app.UseHangfireDashboard();
    }

    app.Services.GetRequiredService<IBackgroundJobClient>().Enqueue<IpSecurityRestrictionUpdater>(handler => handler.UpdateIpRestrictions());
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<IpSecurityRestrictionUpdater>("Update IP restrictions to apimIp and current EventGrid IPs", handler => handler.UpdateIpRestrictions(), Cron.Daily());

    app.Run();
}

static void ConfigureServices(IServiceCollection services, IConfiguration config, IHostEnvironment hostEnvironment)
{
    var connectionString = GetConnectionString(config);

    services.AddHostedService<EdDsaSecurityKeysCacheService>();
    services.Configure<AttachmentStorageOptions>(config.GetSection(key: nameof(AttachmentStorageOptions)));
    services.Configure<AltinnOptions>(config.GetSection(key: nameof(AltinnOptions)));
    services.Configure<AzureResourceManagerOptions>(config.GetSection(key: nameof(AzureResourceManagerOptions)));
    services.Configure<DialogportenSettings>(config.GetSection(key: nameof(DialogportenSettings)));
    services.Configure<IdportenSettings>(config.GetSection(key: nameof(IdportenSettings)));
    services.Configure<GeneralSettings>(config.GetSection(key: nameof(GeneralSettings)));

    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    var altinnOptions = new AltinnOptions();
    config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
    services.AddSingleton<Altinn.Correspondence.Integrations.Slack.SlackExceptionNotificationHandler>();
    services.AddExceptionHandler<SlackExceptionNotificationHandler>();
    services.AddCors(options =>
    {
        options.AddPolicy(name: AuthorizationConstants.ArbeidsflateCors,
                          policy =>
                          {
                              policy.WithOrigins("https://af.tt.altinn.no").SetIsOriginAllowedToAllowWildcardSubdomains();
                              policy.WithMethods("GET", "POST", "DELETE");
                              policy.WithHeaders("Authorization");
                              policy.AllowCredentials();
                          });
    });
    services.ConfigureAuthentication(config, hostEnvironment);
    services.ConfigureAuthorization(config);
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(options =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        options.IncludeXmlComments(xmlPath);
    });
    services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions()
    {
        EnableAdaptiveSampling = false
    });

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
