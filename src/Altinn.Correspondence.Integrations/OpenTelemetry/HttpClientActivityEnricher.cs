using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.Integrations.OpenTelemetry;

public static class HttpClientActivityEnricher
{
    private static readonly Regex GuidRegex = new(
        @"\b[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ResourceIdRegex = new(
        @"/resourceregistry/api/v1/resource/[a-zA-Z0-9\-_]+(?=/|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Enriches HTTP client activities with normalized paths for better grouping in Application Insights
    /// </summary>
    public static void EnrichHttpClientActivity(Activity activity, HttpResponseMessage httpResponse)
    {
        var requestUri = httpResponse.RequestMessage?.RequestUri;
        var responseMethod = httpResponse.RequestMessage?.Method.Method;

        if (requestUri is null)
            return;

        var statusCode = (int)httpResponse.StatusCode;
        var responsePath = requestUri.AbsolutePath;
        var normalizedPath = NormalizeUrlPath(responsePath);

        activity.SetTag("http.method", responseMethod);
        activity.SetTag("http.route", normalizedPath);
        activity.SetTag("http.status_code", statusCode);
        activity.DisplayName = $"{responseMethod} {normalizedPath}";
    }

    /// <summary>
    /// Normalizes URL paths by replacing variable segments with placeholders
    /// </summary>
    public static string NormalizeUrlPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        var normalized = ResourceIdRegex.Replace(path, "/resourceregistry/api/v1/resource/{resourceId}");

        return GuidRegex.Replace(normalized, "{id}");
    }
}