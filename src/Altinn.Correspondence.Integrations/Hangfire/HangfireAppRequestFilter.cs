using Hangfire.Server;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Altinn.Correspondence.Integrations.Hangfire;

public class HangfireAppRequestFilter(TelemetryClient telemetryClient) : IServerFilter
{
    private static AsyncLocal<IDisposable> _contextualLogger;

    private static AsyncLocal<IOperationHolder<RequestTelemetry>> _hangfireAppRequestLogger;

    public void OnPerformed(PerformedContext context)
    {
        telemetryClient.StopOperation(_hangfireAppRequestLogger.Value);
        _contextualLogger.Value?.Dispose();
    }

    public void OnPerforming(PerformingContext context)
    {
        var operationName = new RequestTelemetry
        {
            Name = $"HANGFIRE {context.BackgroundJob.Job.Method.Name}"
        };
        _hangfireAppRequestLogger = new AsyncLocal<IOperationHolder<RequestTelemetry>>((_) => telemetryClient.StartOperation(operationName));
        _contextualLogger = new AsyncLocal<IDisposable>((_) => Serilog.Context.LogContext.PushProperty("JobId", context.BackgroundJob.Id, true));
    }

}
