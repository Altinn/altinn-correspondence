using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;

namespace Altinn.Correspondence.Integrations.OpenTelemetry;

public class HttpDependencyNameProcessor : BaseProcessor<Activity>
{
    private static readonly Regex GuidRegex = new(
        @"\b[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override void OnEnd(Activity activity)
    {
        // Only process HTTP client activities (outgoing requests)
        if (activity.Kind != ActivityKind.Client)
            return;

        // Check if this is an HTTP request
        var httpMethod = activity.GetTagItem("http.method") as string
                        ?? activity.GetTagItem("http.request.method") as string;

        if (string.IsNullOrEmpty(httpMethod))
            return;

        // Get the URL path
        var urlPath = activity.GetTagItem("url.path") as string
                     ?? activity.GetTagItem("http.target") as string;

        if (string.IsNullOrEmpty(urlPath))
            return;

        // Normalize the path by replacing IDs with placeholders
        var normalizedPath = NormalizeUrlPath(urlPath);

        // Set http.route which Azure Monitor uses for dependency name
        activity.SetTag("http.route", normalizedPath);
    }

    private static string NormalizeUrlPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Remove query string if present
        var pathWithoutQuery = path.Split('?')[0];

        // Replace GUIDs/UUIDs
        var normalized = GuidRegex.Replace(pathWithoutQuery, "{id}");

        return normalized;
    }
}