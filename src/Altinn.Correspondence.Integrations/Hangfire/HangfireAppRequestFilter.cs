using Hangfire;
using Hangfire.Client;
using Hangfire.Server;
using Serilog;
using Serilog.Context;

namespace Altinn.Correspondence.Integrations.Hangfire;

/**
 * Filter to make Hangfire jobs app requests in our log tables.
 */
public class HangfireAppRequestFilter : IServerFilter, IClientFilter
{
    public void OnCreated(CreatedContext context)
    {
        Log.Information("Hangfire job {jobId} has been created", context.BackgroundJob.Id);
    }

    public void OnCreating(CreatingContext context)
    {
        Log.Information("Hangfire job being created");
    }

    public void OnPerformed(PerformedContext context)
    {
        Log.Information("Hangfire job {jobId} has been performed", context.BackgroundJob.Id);
    }

    public void OnPerforming(PerformingContext context)
    {
        Log.Information("Hangfire job {jobId} is performing", context.BackgroundJob.Id);
    }
}
