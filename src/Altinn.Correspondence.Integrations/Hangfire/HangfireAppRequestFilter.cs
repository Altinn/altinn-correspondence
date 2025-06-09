using Hangfire.Server;
using System.Diagnostics;

namespace Altinn.Correspondence.Integrations.Hangfire;

public class HangfireAppRequestFilter() : IServerFilter
{
    private static readonly AsyncLocal<IDisposable> _contextualLogger = new();
    private static readonly AsyncLocal<Activity> _hangfireActivity = new();
    private static readonly ActivitySource _activitySource = new("Altinn.Correspondence.Hangfire");

    public void OnPerformed(PerformedContext context)
    {
        _hangfireActivity.Value?.Stop();
        _contextualLogger.Value?.Dispose();
    }

    public void OnPerforming(PerformingContext context)
    {
        var operationName = $"HANGFIRE {context.BackgroundJob.Job.Method.DeclaringType?.Name}.{context.BackgroundJob.Job.Method.Name}";

        var activity = _activitySource.StartActivity(operationName);
        activity?.SetTag("hangfire.job.id", context.BackgroundJob.Id);
        activity?.SetTag("hangfire.job.type", context.BackgroundJob.Job.Method.DeclaringType?.Name);
        activity?.SetTag("hangfire.job.method", context.BackgroundJob.Job.Method.Name);

        _hangfireActivity.Value = activity;
        _contextualLogger.Value = Serilog.Context.LogContext.PushProperty("JobId", context.BackgroundJob.Id, true);
    }
}
