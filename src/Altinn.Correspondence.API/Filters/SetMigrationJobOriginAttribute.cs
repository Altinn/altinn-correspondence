using Altinn.Correspondence.Integrations.Hangfire;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Altinn.Correspondence.API.Filters;

public sealed class SetJobOriginAttribute(string origin) : ActionFilterAttribute
{
    private readonly string _origin = origin;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        BackgroundJobContext.Origin = _origin;
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        BackgroundJobContext.Origin = null;
    }
}


