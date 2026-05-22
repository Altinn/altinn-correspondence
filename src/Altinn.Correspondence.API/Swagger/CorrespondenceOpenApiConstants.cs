namespace Altinn.Correspondence.API.Swagger;

internal static class CorrespondenceOpenApiConstants
{
    public const string DocumentName = "v1";
    public const string Title = "Altinn Correspondence";
    public const string SecuritySchemeId = "JWTBearerAuth";

    public const string Description =
        "Altinn Correspondence API for service owners and recipients. " +
        "All operations require authentication unless otherwise noted. " +
        "Read more about authentication at https://docs.altinn.studio/authorization/getting-started/authentication/";

    public const string BearerDescription =
        "JWT bearer token from Maskinporten (service owner), Altinn token exchange, or Dialogporten (recipient). " +
        "Paste the token to authorize requests from this UI.";

    public const string RoutePrefix = "correspondence/api/v1/swagger";

    /// <summary>
    /// Swagger UI path in the exported OpenAPI document (relative to the correspondence API v1 base in APIM).
    /// </summary>
    public const string SwaggerUiIndexPath = "/swagger/index.html";

    /// <summary>
    /// OpenAPI JSON path in the exported document (relative to the correspondence API v1 base in APIM).
    /// </summary>
    public const string SwaggerJsonPath = $"/swagger/{DocumentName}/swagger.json";

    /// <summary>
    /// Application route where Swagger UI is served.
    /// </summary>
    public static string ApplicationSwaggerUiPath => $"/{RoutePrefix}/index.html";

    /// <summary>
    /// Application route where the OpenAPI JSON document is served.
    /// </summary>
    public static string ApplicationSwaggerJsonPath => $"/{RoutePrefix}/{DocumentName}/swagger.json";
}
