using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.Integrations.OpenTelemetry;

public class NormalizedHttpActivityHandler : DelegatingHandler
{
    private static readonly Regex GuidRegex = new(
        @"\b[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get current activity (created by HttpClient instrumentation)
        var activity = Activity.Current;

        if (activity != null && request.RequestUri != null)
        {
            var path = request.RequestUri.AbsolutePath;
            var normalizedPath = NormalizeUrlPath(path);

            // Set http.route tag before the activity is processed
            activity.SetTag("http.route", normalizedPath);
            activity.DisplayName = $"{request.Method} {normalizedPath}";
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static string NormalizeUrlPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        return GuidRegex.Replace(path, "{id}");
    }
}