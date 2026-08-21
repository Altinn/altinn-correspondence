using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Hosting;

namespace Altinn.Correspondence.Integrations.OpenTelemetry;

/// <summary>
/// Surfaces OpenTelemetry SDK / Azure Monitor exporter diagnostics as App Insights metrics
/// (outside the log batch pipeline that may itself be dropping records).
/// </summary>
public sealed class OpenTelemetryDiagnosticsListener : EventListener, IHostedService
{
    public const string MeterName = "Altinn.Correspondence.OpenTelemetry.Diagnostics";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _diagnosticEvents;

    public OpenTelemetryDiagnosticsListener()
    {
        _diagnosticEvents = _meter.CreateCounter<long>(
            name: "otel.diagnostics.events",
            unit: "{event}",
            description: "OpenTelemetry SDK and Azure Monitor exporter diagnostic events (buffer drops, export failures, etc.).");
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        _meter.Dispose();
        return Task.CompletedTask;
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name is "OpenTelemetry-Sdk" or "OpenTelemetry-AzureMonitor-Exporter")
        {
            EnableEvents(eventSource, EventLevel.Warning, EventKeywords.All);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        var source = eventData.EventSource?.Name;
        if (source is not ("OpenTelemetry-Sdk" or "OpenTelemetry-AzureMonitor-Exporter"))
        {
            return;
        }

        // Event 32: "'{0}' exporting to '{1}' dropped '{2}' item(s) due to buffer full."
        var isBufferFullDrop = source == "OpenTelemetry-Sdk" && eventData.EventId == 32;

        _diagnosticEvents.Add(
            1,
            new KeyValuePair<string, object?>("source", source),
            new KeyValuePair<string, object?>("event_id", eventData.EventId),
            new KeyValuePair<string, object?>("event_name", eventData.EventName ?? string.Empty),
            new KeyValuePair<string, object?>("buffer_full_drop", isBufferFullDrop));
    }
}
