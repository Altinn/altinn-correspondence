using Microsoft.Extensions.Primitives;

namespace Altinn.Correspondence.Helpers;
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", new StringValues("nosniff"));
        if (!IsSwaggerPath(context.Request.Path))
        {
            context.Response.Headers.Append("Cache-Control", new StringValues("no-store"));
        }

        await _next(context);
    }

    private static bool IsSwaggerPath(PathString path) =>
        path.StartsWithSegments("/correspondence/swagger", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
}