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

        // Check if this is an HTTP request using new semantic conventions
        var httpMethod = activity.GetTagItem("http.request.method") as string
                        ?? activity.GetTagItem("http.method") as string;

        if (string.IsNullOrEmpty(httpMethod))
            return;

        // Get the full URL from the new semantic convention
        var urlFull = activity.GetTagItem("url.full") as string
                     ?? activity.GetTagItem("http.url") as string;

        if (string.IsNullOrEmpty(urlFull))
            return;

        // Parse the URL and extract the path
        if (!Uri.TryCreate(urlFull, UriKind.Absolute, out var uri))
            return;

        var path = uri.AbsolutePath; // This gets just the path without query string

        // Normalize the path by replacing IDs with placeholders
        var normalizedPath = NormalizeUrlPath(path);

        // Set http.route which should be used by Azure Monitor
        activity.SetTag("http.route", normalizedPath);

        // Also update DisplayName
        activity.DisplayName = $"{httpMethod} {normalizedPath}";

        Console.WriteLine($"Set http.route to: {normalizedPath}");
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
