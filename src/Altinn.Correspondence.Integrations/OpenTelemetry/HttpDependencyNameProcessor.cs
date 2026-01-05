using OpenTelemetry;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.Integrations.OpenTelemetry;

public class HttpDependencyNameProcessor : BaseProcessor<Activity>
{
    private static readonly Regex GuidRegex = new(
        @"\b[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


    private static readonly FieldInfo? OperationNameField = typeof(Activity)
        .GetField("_operationName", BindingFlags.Instance | BindingFlags.NonPublic);

    public override void OnEnd(Activity activity)
    {
        // Only process HTTP client activities (outgoing requests)
        if (activity.Kind != ActivityKind.Client)
            return;

        var httpMethod = activity.GetTagItem("http.request.method") as string
                        ?? activity.GetTagItem("http.method") as string;

        if (string.IsNullOrEmpty(httpMethod))
            return;

        var urlFull = activity.GetTagItem("url.full") as string
                     ?? activity.GetTagItem("http.url") as string;

        if (string.IsNullOrEmpty(urlFull))
            return;

        if (!Uri.TryCreate(urlFull, UriKind.Absolute, out var uri))
            return;

        var path = uri.AbsolutePath; 

        // Normalize the path by replacing IDs with placeholders
        var normalizedPath = NormalizeUrlPath(path);

        activity.DisplayName = $"{httpMethod} {normalizedPath}";
        if (OperationNameField != null)
        {
            Console.WriteLine("Setting operation name field to " + activity.DisplayName);
            OperationNameField.SetValue(activity, activity.DisplayName);
        } else
        {
            Console.WriteLine("Could not find operation name field");
        }
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
