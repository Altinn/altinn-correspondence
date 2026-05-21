namespace Altinn.Correspondence.API.Middleware;

/// <summary>
/// Applies caching headers on OpenAPI JSON so repeat loads behave sensibly across environments.
/// </summary>
internal sealed class SwaggerDocumentCacheMiddleware(RequestDelegate next, IHostEnvironment hostEnvironment)
{
    private static readonly PathString CorrespondenceSwaggerPathPrefix = new("/correspondence/swagger");
    private static readonly PathString LegacySwaggerPathPrefix = new("/swagger");

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSwaggerDocumentRequest(context.Request.Path))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = hostEnvironment.IsDevelopment()
                    ? "no-store, no-cache"
                    : "public, max-age=600";

                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    private static bool IsSwaggerDocumentRequest(PathString path) =>
        (path.StartsWithSegments(CorrespondenceSwaggerPathPrefix, StringComparison.OrdinalIgnoreCase)
         || path.StartsWithSegments(LegacySwaggerPathPrefix, StringComparison.OrdinalIgnoreCase))
        && path.Value?.EndsWith("swagger.json", StringComparison.OrdinalIgnoreCase) == true;
}
