using Hangfire.Server;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Altinn.Correspondence.Integrations.Hangfire;

public class HangfireAppRequestFilter(TelemetryClient telemetryClient) : IServerFilter
{
    public static string JobId
    {
        get { return _jobId; }
        set { _jobId = value; }
    }

    [ThreadStatic]
    private static string _jobId;

    [ThreadStatic]
    private static IDisposable _contextualLogger;

    [ThreadStatic]
    private static IOperationHolder<RequestTelemetry> _hangfireAppRequestLogger;

    public void OnPerformed(PerformedContext context)
    {
        telemetryClient.StopOperation(_hangfireAppRequestLogger);
        _contextualLogger?.Dispose();
    }

    public void OnPerforming(PerformingContext context)
    {
        var operationName = new RequestTelemetry
        {
            Name = $"{context.BackgroundJob.Job.Method.Name}"
        };
        _hangfireAppRequestLogger = telemetryClient.StartOperation(operationName);
        _contextualLogger = Serilog.Context.LogContext.PushProperty("JobId", JobId, true);
    }

}
