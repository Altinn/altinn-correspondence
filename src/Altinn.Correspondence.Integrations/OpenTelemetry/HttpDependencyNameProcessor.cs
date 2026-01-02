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
        // Only process HTTP client activities
        if (activity.Kind != ActivityKind.Client)
            return;

        // Check if this is an HTTP dependency
        var httpMethod = activity.GetTagItem("http.method") as string
                        ?? activity.GetTagItem("http.request.method") as string;

        if (string.IsNullOrEmpty(httpMethod))
            return;

        var url = activity.GetTagItem("url.full") as string
                 ?? activity.GetTagItem("http.url") as string;

        if (string.IsNullOrEmpty(url))
            return;

        var uri = new Uri(url);
        var normalizedPath = NormalizeUrlPath(uri.PathAndQuery);

        // Set the display name that Azure Monitor will use as the dependency name
        activity.DisplayName = $"{httpMethod} {normalizedPath}";

        // Optionally set http.route for additional grouping
        activity.SetTag("http.route", normalizedPath);
    }

    private static string NormalizeUrlPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Replace GUIDs/UUIDs
        var normalized = GuidRegex.Replace(path, "{id}");

        return normalized;
    }
}