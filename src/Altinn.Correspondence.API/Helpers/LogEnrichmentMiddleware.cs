using Serilog.Context;
using Serilog.Core.Enrichers;

namespace Altinn.Correspondence.API.Helpers;

/**
 * Used for enriching the log context with properties that are relevant for the HTTP request.
 **/
public class LogEnrichmentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Dictionary<string, object> _properties = new();

    public LogEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Items["LogProperties"] = _properties;

        using var logScope = CreateLogScope(_properties);
        await _next(context);
    }

    private static IDisposable CreateLogScope(Dictionary<string, object> properties)
    {
        return LogContext.Push(new PropertyEnricher(properties));
    }
}
