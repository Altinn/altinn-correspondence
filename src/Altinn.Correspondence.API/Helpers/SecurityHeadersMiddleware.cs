using Microsoft.Extensions.Primitives;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Add("X-Content-Type-Options", new StringValues("nosniff"));
        context.Response.Headers.Add("Cache-Control", new StringValues("no-store"));

        await _next(context);
    }
}