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
using System.Net.Http;

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
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            var method = request.Method.ToString();
                            var uri = request.RequestUri;

                            if (uri != null)
                            {
                                var normalizedPath = NormalizeUrlPath(uri.PathAndQuery);
                                var displayName = $"{method} {uri.Host}{normalizedPath}";

                                // Set the operation name that Application Insights will use
                                activity.DisplayName = displayName;

                                // Also set the http.route tag for better grouping
                                activity.SetTag("http.route", normalizedPath);
                            }
                        };
                    })
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
    private static string NormalizeUrlPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Replace GUIDs/UUIDs (with or without dashes)
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            path,
            @"\b[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}\b",
            "{id}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove query parameters
        normalized = normalized.Split('?').FirstOrDefault();

        return normalized;
    }
}
