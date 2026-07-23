namespace Altinn.Correspondence.Integrations.OpenTelemetry;

public static class TelemetryFilterHelper
{
    /// <summary>
    /// Determines if a request should be excluded from telemetry based on path.
    /// Returns true if the request should be EXCLUDED.
    /// </summary>
    public static bool ShouldExcludeRequest(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var pathSpan = path.AsSpan();
        
        // Strip query parameters
        int queryIndex = pathSpan.IndexOf('?');
        if (queryIndex >= 0)
        {
            pathSpan = pathSpan.Slice(0, queryIndex);
        }

        // Always exclude health checks (exact match)
        if (pathSpan.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            pathSpan.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}