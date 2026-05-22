using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Swagger;

/// <summary>
/// Hides the decorated controller or action from the public OpenAPI document in non-Development environments.
/// In Development, all such endpoints are included in Swagger for local use.
/// For endpoints that must never appear in Swagger (e.g. health checks), use <see cref="ExcludeFromSwaggerAttribute"/> instead.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HideFromPublicApiAttribute : ApiExplorerSettingsAttribute
{
    public HideFromPublicApiAttribute()
    {
        IgnoreApi = true;
    }
}
