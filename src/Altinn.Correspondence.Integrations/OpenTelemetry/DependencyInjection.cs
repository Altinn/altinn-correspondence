using Altinn.Correspondence.Core.Options;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Altinn.Correspondence.Integrations.OpenTelemetry;

public static class DependencyInjection
{
    public static IServiceCollection ConfigureOpenTelemetry(
        this IServiceCollection services,
        GeneralSettings generalSettings)
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            KeyValuePair.Create("service.name", (object)"altinn-correspondence"),
        };

        services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder.AddAttributes(attributes))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http")
                    .AddNpgsqlInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("Altinn.Correspondence.Integrations.Hangfire")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = httpContext =>
                        {
                            if (httpContext.Request.Method == "OPTIONS")
                            {
                                return false;
                            }
                            var path = httpContext.Request.Path.Value?.ToLowerInvariant();
                            return path != null &&
                                   !path.Contains("/health") &&
                                   !path.Contains("/migration");
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new RequestFilterProcessor(generalSettings, new HttpContextAccessor()));
            })
            .WithLogging(logging =>
            {
            });

        if (!string.IsNullOrWhiteSpace(generalSettings.ApplicationInsightsConnectionString))
        {
            services.ConfigureOpenTelemetryMeterProvider(metrics =>
                metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = generalSettings.ApplicationInsightsConnectionString));

            services.ConfigureOpenTelemetryTracerProvider(tracing =>
                tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = generalSettings.ApplicationInsightsConnectionString));

            services.ConfigureOpenTelemetryLoggerProvider(logging =>
                logging.AddAzureMonitorLogExporter(o => o.ConnectionString = generalSettings.ApplicationInsightsConnectionString));
        }

        return services;
    }
}
