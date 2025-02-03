using Serilog.Context;
using Serilog.Core.Enrichers;

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
        var properties = new Dictionary<string, object>();
        context.Items["LogProperties"] = properties;

        using var logScope = CreateLogScope(properties);
        await _next(context);
    }

    private static IDisposable CreateLogScope(Dictionary<string, object> properties)
    {
        return LogContext.Push(new PropertyEnricher(properties));
    }
}
