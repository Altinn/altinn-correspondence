using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Altinn.Correspondence.Integrations.Azure;

public static class AzureMonitorTelemetryConfiguration
{
    public static IServiceCollection AddAzureMonitorTelemetryExporters(
        this IServiceCollection services, 
        string applicationInsightsConnectionString, 
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
        {
            logger.LogWarning("ApplicationInsightsConnectionString is not set, skipping Azure Monitor telemetry exporters.");
            return services;
        }

        services.Configure<OpenTelemetryLoggerOptions>(logging => 
            logging.AddAzureMonitorLogExporter(o => o.ConnectionString = applicationInsightsConnectionString));
        
        services.ConfigureOpenTelemetryMeterProvider(metrics => 
            metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = applicationInsightsConnectionString));
        
        services.ConfigureOpenTelemetryTracerProvider(tracing => 
            tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = applicationInsightsConnectionString));

        return services;
    }
} 