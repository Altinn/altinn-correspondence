using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Swagger;

/// <summary>
/// Permanently excludes the decorated controller or action from OpenAPI/Swagger in every environment,
/// including Development. Use for infrastructure endpoints such as health checks that must not appear in APIM.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ExcludeFromSwaggerAttribute : ApiExplorerSettingsAttribute
{
    public ExcludeFromSwaggerAttribute()
    {
        IgnoreApi = true;
    }
}
