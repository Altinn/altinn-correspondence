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
}
