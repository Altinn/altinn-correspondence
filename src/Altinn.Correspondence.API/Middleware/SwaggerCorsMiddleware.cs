using Altinn.Correspondence.API.Swagger;
using Microsoft.Extensions.Primitives;

namespace Altinn.Correspondence.API.Middleware;

/// <summary>
/// Applies CORS headers for Swagger UI and OpenAPI document requests so they can be loaded cross-origin when needed.
/// </summary>
internal sealed class SwaggerCorsMiddleware(RequestDelegate next, CorrespondenceCorsMetadata corsMetadata)
{
    private static readonly PathString SwaggerPathPrefix = new("/correspondence/api/v1/swagger");

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(SwaggerPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var origin = context.Request.Headers.Origin.ToString();
        if (CorrespondenceCorsOrigins.IsAllowedOrigin(origin, corsMetadata))
        {
            context.Response.Headers.Append("Access-Control-Allow-Origin", new StringValues(origin));
            context.Response.Headers.Append("Access-Control-Allow-Credentials", new StringValues("true"));
            context.Response.Headers.Append("Vary", new StringValues("Origin"));
        }

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            if (CorrespondenceCorsOrigins.IsAllowedOrigin(origin, corsMetadata))
            {
                context.Response.Headers.Append(
                    "Access-Control-Allow-Methods",
                    new StringValues(string.Join(", ", corsMetadata.AllowedMethods)));
                context.Response.Headers.Append(
                    "Access-Control-Allow-Headers",
                    new StringValues(string.Join(", ", corsMetadata.AllowedHeaders)));
            }

            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await next(context);
    }
}
