using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Altinn.Correspondence.API.Swagger;

internal static class OpenApiDocumentationHelper
{
    internal static bool IsExcludedFromPublicOpenApi(ApiDescription description) =>
        description.ActionDescriptor.EndpointMetadata.OfType<ExcludeFromPublicOpenApiAttribute>().Any();
}
