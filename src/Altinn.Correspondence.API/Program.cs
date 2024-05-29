using Altinn.Correspondence.Application;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Persistence;
using Azure.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
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
    app.UseAuthorization();

    app.MapControllers();

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
    }


    app.Run();
}

static void ConfigureServices(IServiceCollection services, IConfiguration config, IHostEnvironment hostEnvironment)
{
    services.Configure<AttachmentStorageOptions>(config.GetSection(key: nameof(AttachmentStorageOptions)));

    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();
    services.AddApplicationInsightsTelemetry();

    services.AddApplicationHandlers();
    services.AddPersistence();

    services.AddHttpClient();
    services.AddProblemDetails();

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

    services.AddDbContext<ApplicationDbContext>(opts =>
    {
        var connectionString = config.GetSection("DatabaseOptions:ConnectionString").Value ?? Environment.GetEnvironmentVariable("DatabaseOptions__ConnectionString");
        if (string.IsNullOrWhiteSpace(new NpgsqlConnectionStringBuilder(connectionString).Password))
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
            var token = credential
                .GetToken(
                    new Azure.Core.TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" })
                );
            connectionString += ";Password=" + token.Token + ";";
        }
        opts.UseNpgsql(connectionString);
    });
}

public partial class Program { }
