using Hangfire.Client;
using Hangfire.Common;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Altinn.Correspondence.Integrations.Hangfire;

public class BackgroundJobClientFilter(ILogger<BackgroundJobClientFilter> logger) : JobFilterAttribute, IClientFilter
{
    private const string CreateTimerKey = "BackgroundJobClientFilter.CreateTimer";
    private const int SlowCreateThresholdMs = 500;

    /// <summary>
    /// Set the Origin parameter on the new job if the background job context has an origin
    /// </summary>
    public void OnCreating(CreatingContext filterContext)
    {
        filterContext.Items[CreateTimerKey] = Stopwatch.StartNew();
        var origin = BackgroundJobContext.Origin;
        if (!string.IsNullOrEmpty(origin))
        {
            // Set Origin parameter on new job if not present
            filterContext.SetJobParameter("Origin", origin);
        }
    }

    public void OnCreated(CreatedContext filterContext)
    {
        if (!filterContext.Items.TryGetValue(CreateTimerKey, out var timerValue) || timerValue is not Stopwatch timer)
        {
            return;
        }

        timer.Stop();
        if (timer.ElapsedMilliseconds < SlowCreateThresholdMs)
        {
            return;
        }

        logger.LogWarning(
            "Slow Hangfire job creation: {ElapsedMs} ms. Job: {JobType}.{JobMethod}, Queue: {Queue}, CreatedJobId: {CreatedJobId}",
            timer.ElapsedMilliseconds,
            filterContext.Job.Method.DeclaringType?.Name ?? "UnknownType",
            filterContext.Job.Method.Name,
            "unknown",
            filterContext.BackgroundJob?.Id ?? "unknown");
    }
}


