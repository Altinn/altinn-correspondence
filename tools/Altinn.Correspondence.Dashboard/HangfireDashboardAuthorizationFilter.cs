using Hangfire.Annotations;
using Hangfire.Dashboard;

internal class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    // Dummy implementation. Handled by Container App.
    public bool Authorize([NotNull] DashboardContext context)
    {
        return true;
    }
}