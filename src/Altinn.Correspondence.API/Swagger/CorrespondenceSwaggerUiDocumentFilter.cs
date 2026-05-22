using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Swagger;

/// <summary>
/// Adds Swagger UI and OpenAPI document routes to the specification when running locally (Development).
/// Paths are relative to the correspondence API v1 base (for example /swagger/index.html) and are omitted from the public APIM spec.
/// </summary>
internal sealed class CorrespondenceSwaggerUiDocumentFilter(IHostEnvironment environment) : IDocumentFilter
{
    public const string DocumentationTag = "Documentation";
    public const string StaticAssetsExtensionName = "x-altinn-swagger-static-assets";

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        swaggerDoc.Paths ??= new OpenApiPaths();

        AddPathIfMissing(swaggerDoc.Paths, CorrespondenceOpenApiConstants.SwaggerUiIndexPath, CreateSwaggerUiOperation());
        AddPathIfMissing(swaggerDoc.Paths, CorrespondenceOpenApiConstants.SwaggerJsonPath, CreateOpenApiDocumentOperation());
        AddPathIfMissing(
            swaggerDoc.Paths,
            CorrespondenceOpenApiConstants.SwaggerStaticAssetPath,
            CreateStaticAssetOperation());

        ApplyStaticAssetsExtension(swaggerDoc);

        if (swaggerDoc.Tags is null)
        {
            swaggerDoc.Tags = new HashSet<OpenApiTag>();
        }

        if (!swaggerDoc.Tags.Any(tag => tag.Name == DocumentationTag))
        {
            swaggerDoc.Tags.Add(new OpenApiTag
            {
                Name = DocumentationTag,
                Description = "Hosted API documentation (Swagger UI, static assets, and OpenAPI JSON)."
            });
        }
    }

    private static void ApplyStaticAssetsExtension(OpenApiDocument swaggerDoc)
    {
        swaggerDoc.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        swaggerDoc.Extensions[StaticAssetsExtensionName] = new AltinnSwaggerStaticAssetsOpenApiExtension(
            CorrespondenceOpenApiConstants.SwaggerStaticAssetPath,
            CorrespondenceOpenApiConstants.KnownSwaggerStaticAssets,
            CorrespondenceOpenApiConstants.RoutePrefix);
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

    private static OpenApiOperation CreateStaticAssetOperation()
    {
        var knownAssets = string.Join(", ", CorrespondenceOpenApiConstants.KnownSwaggerStaticAssets);

        return new OpenApiOperation
        {
            Tags = new HashSet<OpenApiTagReference> { new(DocumentationTag) },
            Summary = "Swagger UI static asset",
            Description =
                "Catch-all route for Swagger UI static files (css, js, images) under /"
                + CorrespondenceOpenApiConstants.RoutePrefix
                + "/. OpenAPI cannot express a true wildcard; this path parameter matches one file name segment. "
                + "Typical values include: " + knownAssets + ". "
                + "The application route is /" + CorrespondenceOpenApiConstants.RoutePrefix + "/{asset}.",
            OperationId = "documentation_swagger_static_asset",
            Parameters =
            [
                new OpenApiParameter
                {
                    Name = CorrespondenceOpenApiConstants.SwaggerStaticAssetParameterName,
                    In = ParameterLocation.Path,
                    Required = true,
                    Description = "File name of a Swagger UI static asset (for example swagger-ui-bundle.js).",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String
                    }
                }
            ],
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "Static asset content.",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["text/css"] = new OpenApiMediaType(),
                        ["application/javascript"] = new OpenApiMediaType(),
                        ["text/html"] = new OpenApiMediaType(),
                        ["image/png"] = new OpenApiMediaType(),
                        ["image/x-icon"] = new OpenApiMediaType()
                    }
                }
            }
        };
    }

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
