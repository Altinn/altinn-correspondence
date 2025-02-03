using Serilog.Context;

namespace Altinn.Correspondence.API.Helpers;

/**
 * Used for enriching the log context with properties that are relevant for the HTTP request.
 **/
public class LogEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public LogEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        {
            context.Items["LogContextDisposables"] = new List<IDisposable>();
            try
            {
                await _next(context);
            }
            finally
            {
                if (context.Items["LogContextDisposables"] is List<IDisposable> disposables)
                {
                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }
    }
}
