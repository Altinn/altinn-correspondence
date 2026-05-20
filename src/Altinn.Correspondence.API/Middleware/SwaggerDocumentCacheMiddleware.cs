namespace Altinn.Correspondence.API.Middleware;

/// <summary>
/// Applies cache-friendly headers to OpenAPI documents so repeated loads do not hit the generator on every request.
/// </summary>
internal sealed class SwaggerDocumentCacheMiddleware(RequestDelegate next)
{
    private static readonly PathString SwaggerPathPrefix = new("/correspondence/swagger");

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSwaggerDocumentRequest(context.Request.Path))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "public, max-age=600";
                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    private static bool IsSwaggerDocumentRequest(PathString path) =>
        path.StartsWithSegments(SwaggerPathPrefix, StringComparison.OrdinalIgnoreCase)
        && path.Value?.EndsWith("swagger.json", StringComparison.OrdinalIgnoreCase) == true;
}
