using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Swagger;

/// <summary>
/// Adds Swagger UI and OpenAPI document routes to the specification for APIM import.
/// Paths are relative to the correspondence API v1 base (for example /swagger/index.html).
/// </summary>
internal sealed class CorrespondenceSwaggerUiDocumentFilter : IDocumentFilter
{
    public const string DocumentationTag = "Documentation";

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Paths ??= new OpenApiPaths();

        AddPathIfMissing(swaggerDoc.Paths, CorrespondenceOpenApiConstants.SwaggerUiIndexPath, CreateSwaggerUiOperation());
        AddPathIfMissing(swaggerDoc.Paths, CorrespondenceOpenApiConstants.SwaggerJsonPath, CreateOpenApiDocumentOperation());

        if (swaggerDoc.Tags is null)
        {
            swaggerDoc.Tags = new HashSet<OpenApiTag>();
        }

        if (!swaggerDoc.Tags.Any(tag => tag.Name == DocumentationTag))
        {
            swaggerDoc.Tags.Add(new OpenApiTag
            {
                Name = DocumentationTag,
                Description = "Hosted API documentation (Swagger UI and OpenAPI JSON)."
            });
        }
    }

    private static void AddPathIfMissing(OpenApiPaths paths, string path, OpenApiOperation operation)
    {
        if (paths.ContainsKey(path))
        {
            return;
        }

        paths.Add(path, new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = operation
            }
        });
    }

    private static OpenApiOperation CreateSwaggerUiOperation() =>
        CreateDocumentationOperation(
            summary: "Swagger UI",
            description: "Interactive API documentation. The application serves this at "
                + CorrespondenceOpenApiConstants.ApplicationSwaggerUiPath + ".",
            mediaType: "text/html");

    private static OpenApiOperation CreateOpenApiDocumentOperation() =>
        CreateDocumentationOperation(
            summary: "OpenAPI document",
            description: "Machine-readable API specification. The application serves this at "
                + CorrespondenceOpenApiConstants.ApplicationSwaggerJsonPath + ".",
            mediaType: "application/json");

    private static OpenApiOperation CreateDocumentationOperation(string summary, string description, string mediaType)
    {
        return new OpenApiOperation
        {
            Tags = new HashSet<OpenApiTagReference> { new(DocumentationTag) },
            Summary = summary,
            Description = description,
            OperationId = "documentation_" + summary.Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant(),
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "OK",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [mediaType] = new OpenApiMediaType()
                    }
                }
            }
        };
    }
}
