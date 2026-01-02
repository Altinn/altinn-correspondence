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

        var httpMethod = activity.GetTagItem("http.method") as string
                        ?? activity.GetTagItem("http.request.method") as string;

        if (string.IsNullOrEmpty(httpMethod))
            return;

        // Get the path from the tags that Azure Monitor uses
        var httpTarget = activity.GetTagItem("http.target") as string;
        var urlPath = activity.GetTagItem("url.path") as string;

        var pathToNormalize = httpTarget ?? urlPath;

        if (string.IsNullOrEmpty(pathToNormalize))
            return;

        var normalizedPath = NormalizeUrlPath(pathToNormalize);

        // Update the tags that Azure Monitor uses to construct the dependency name
        if (httpTarget != null)
        {
            activity.SetTag("http.target", normalizedPath);
        }
        if (urlPath != null)
        {
            activity.SetTag("url.path", normalizedPath);
        }

        // Also set DisplayName and http.route
        activity.DisplayName = $"{httpMethod} {normalizedPath}";
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