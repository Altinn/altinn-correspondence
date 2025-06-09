using Altinn.Correspondence.Core.Options;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Altinn.Correspondence.Integrations.Azure;

public static class OpenTelemetryConfiguration
{
    public static WebApplicationBuilder ConfigureOpenTelemetry(
        this WebApplicationBuilder builder,
        string applicationInsightsConnectionString,
        ILogger logger)
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            KeyValuePair.Create("service.name", (object)"altinn-correspondence"),
        };

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder.AddAttributes(attributes))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(
                    "Microsoft.AspNetCore.Hosting",
                    "Microsoft.AspNetCore.Server.Kestrel",
                    "System.Net.Http");
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = httpContext =>
                    {
                        var path = httpContext.Request.Path.Value?.ToLowerInvariant();
                        return path != null && 
                               !path.Contains("/health") && 
                               !path.Contains("/migration");
                    };
                });
                tracing.AddHttpClientInstrumentation();
                tracing.AddNpgsql();
            });

        if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
        {
            builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => 
                logging.AddAzureMonitorLogExporter(o => o.ConnectionString = applicationInsightsConnectionString));

            builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => 
                metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = applicationInsightsConnectionString));

            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => 
                tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = applicationInsightsConnectionString));
        }

        return builder;
    }
}
