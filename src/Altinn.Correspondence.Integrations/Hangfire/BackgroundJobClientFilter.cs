using Hangfire.Client;
using Hangfire.Common;

namespace Altinn.Correspondence.Integrations.Hangfire;

public class BackgroundJobClientFilter : JobFilterAttribute, IClientFilter
{
    /// <summary>
    /// Set the Origin parameter on the new job if the background job context has an origin
    /// </summary>
    public void OnCreating(CreatingContext filterContext)
    {
        var origin = BackgroundJobContext.Origin;
        if (!string.IsNullOrEmpty(origin))
        {
            // Set Origin parameter on new job if not present
            filterContext.SetJobParameter("Origin", origin);
        }
    }

    public void OnCreated(CreatedContext filterContext)
    {
        // no-op
    }
}


