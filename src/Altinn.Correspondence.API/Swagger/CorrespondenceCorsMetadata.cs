using Altinn.Correspondence.Core.Options;

namespace Altinn.Correspondence.API.Swagger;

internal sealed record CorrespondenceCorsMetadata(
    IReadOnlyList<string> AllowedOrigins,
    IReadOnlyList<string> AllowedMethods,
    IReadOnlyList<string> AllowedHeaders,
    bool AllowCredentials);

internal static class CorrespondenceCorsOrigins
{
    public const string OpenApiExtensionName = "x-altinn-cors";
    public const string OperationExtensionName = "x-cors-enabled";

    public static readonly string[] AllowedMethods = ["GET", "POST", "DELETE", "OPTIONS"];

    public static readonly string[] AllowedHeaders =
        ["Authorization", "request-id", "request-context", "traceparent"];

    public static CorrespondenceCorsMetadata CreateMetadata(AltinnOptions altinnOptions, GeneralSettings generalSettings)
    {
        return new CorrespondenceCorsMetadata(
            GetAllowedOrigins(altinnOptions, generalSettings),
            AllowedMethods,
            AllowedHeaders,
            AllowCredentials: true);
    }

    public static string[] GetAllowedOrigins(AltinnOptions altinnOptions, GeneralSettings generalSettings)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var origin in altinnOptions.ArbeidsflateOriginsCommaSeparated.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            origins.Add(origin);
        }

        if (TryGetOriginFromBaseUrl(generalSettings.CorrespondenceBaseUrl, out var platformOrigin))
        {
            origins.Add(platformOrigin);
        }

        return origins.OrderBy(origin => origin, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsAllowedOrigin(string? origin, CorrespondenceCorsMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        return metadata.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetOriginFromBaseUrl(string? baseUrl, out string origin)
    {
        origin = string.Empty;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(baseUrl.TrimEnd('/'), UriKind.Absolute, out var uri))
        {
            return false;
        }

        origin = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }
}
