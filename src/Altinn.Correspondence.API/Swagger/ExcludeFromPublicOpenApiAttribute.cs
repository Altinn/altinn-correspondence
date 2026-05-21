namespace Altinn.Correspondence.API.Swagger;

/// <summary>
/// Marker for endpoints/controllers that stay in the explorer but must not appear in the public (<c>v1</c>) OpenAPI document.
/// They are included in APIM-facing documents (<c>apim</c> and <c>apim-*</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class ExcludeFromPublicOpenApiAttribute : Attribute;
