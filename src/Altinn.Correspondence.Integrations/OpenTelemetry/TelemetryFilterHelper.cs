using Altinn.Correspondence.Core.Options;

namespace Altinn.Correspondence.Integrations.OpenTelemetry;

public static class TelemetryFilterHelper
{
    /// <summary>
    /// Determines if a request should be excluded from telemetry based on path and settings.
    /// Returns true if the request should be EXCLUDED.
    /// </summary>
    public static bool ShouldExcludeRequest(string? path, GeneralSettings settings)
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
        if (pathSpan.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Exclude sync calls if disabled (check before migration since it's more specific)
        if (pathSpan.Contains("/correspondence/api/v1/migration/correspondence/sync".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return settings.DisableTelemetryForSync;
        }

        // Exclude migration calls if disabled
        if (pathSpan.Contains("/correspondence/api/v1/migration/correspondence".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || pathSpan.Contains("/correspondence/api/v1/migration/makemigratedcorrespondenceavailable".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || pathSpan.Contains("/correspondence/api/v1/migration/attachment".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return settings.DisableTelemetryForMigration;
        }

        return false;
    }
}