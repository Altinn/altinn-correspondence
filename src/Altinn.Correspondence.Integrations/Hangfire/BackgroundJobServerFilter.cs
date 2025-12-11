using Hangfire.Common;
using Hangfire.Server;

namespace Altinn.Correspondence.Integrations.Hangfire;

public class BackgroundJobServerFilter : JobFilterAttribute, IServerFilter
{
    /// <summary>
    /// Set the background job context origin if the job has an Origin parameter.
    /// </summary>
    public void OnPerforming(PerformingContext context)
    {
        var origin = context.GetJobParameter<string>("Origin");
        if (!string.IsNullOrEmpty(origin))
        {
            BackgroundJobContext.Origin = origin;
        }
    }

    public void OnPerformed(PerformedContext context)
    {
        // Clear after job completes
        BackgroundJobContext.Origin = null;
    }
}


