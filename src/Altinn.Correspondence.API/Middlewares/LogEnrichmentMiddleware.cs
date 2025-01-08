using Serilog.Context;

namespace Altinn.Correspondence.API.Middlewares;

public class LogEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public LogEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Create a scope that will last for the entire request
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        {
            // Store the LogContext.PushProperty disposables in the HttpContext items
            // so we can add more properties during the request
            context.Items["LogContextDisposables"] = new List<IDisposable>();

            try
            {
                await _next(context);
            }
            finally
            {
                // Clean up any disposables we created
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
