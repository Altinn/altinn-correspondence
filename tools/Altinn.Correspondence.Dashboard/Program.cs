using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Persistence;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true)
    .AddJsonFile("appsettings.local.json", true, true);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions()
{
    EnableAdaptiveSampling = false
});
builder.Services.AddSingleton<IConnectionFactory, HangfireConnectionFactory>();
builder.Services.AddHangfire((provider, config) =>
    {
        config.UsePostgreSqlStorage(
            c => c.UseConnectionFactory(provider.GetService<IConnectionFactory>())
        );
        config.UseSerilogLogProvider();
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseHangfireDashboard("/hangfire", new DashboardOptions()
{
    Authorization = [new HangfireDashboardAuthorizationFilter()]
});
app.MapGet("/", () => Results.Redirect("/hangfire"));

app.Run();