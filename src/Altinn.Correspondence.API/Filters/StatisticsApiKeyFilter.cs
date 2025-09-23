using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace Altinn.Correspondence.API.Filters;

/// <summary>
/// Authorization filter to validate API key for statistics endpoints
/// </summary>
public class StatisticsApiKeyFilter : IAuthorizationFilter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StatisticsApiKeyFilter> _logger;

    public StatisticsApiKeyFilter(IConfiguration configuration, ILogger<StatisticsApiKeyFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Only apply to statistics endpoints
        if (!context.HttpContext.Request.Path.StartsWithSegments("/correspondence/api/v1/statistics"))
        {
            return; // Not a statistics endpoint, let it pass
        }

        _logger.LogDebug("Statistics endpoint accessed, validating API key");

        // Check if API key is provided
        if (!context.HttpContext.Request.Headers.TryGetValue("X-API-Key", out StringValues apiKeyHeader))
        {
            _logger.LogWarning("Statistics endpoint accessed without API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
            context.Result = new UnauthorizedObjectResult(new { error = "API key required for statistics endpoints" });
            return;
        }

        var providedApiKey = apiKeyHeader.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            _logger.LogWarning("Statistics endpoint accessed with empty API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
            context.Result = new UnauthorizedObjectResult(new { error = "API key cannot be empty" });
            return;
        }

        // Get configured API key
        var configuredApiKey = _configuration["StatisticsApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            _logger.LogError("StatisticsApiKey is not configured in application settings");
            context.Result = new UnauthorizedObjectResult(new { error = "API key validation not configured" });
            return;
        }

        // Validate API key using constant-time comparison
        if (!SecureEquals(providedApiKey, configuredApiKey))
        {
            _logger.LogWarning("Statistics endpoint accessed with invalid API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid API key" });
            return;
        }

        _logger.LogInformation("Statistics endpoint accessed with valid API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP first (in case of proxy/load balancer)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            return forwardedFor.FirstOrDefault()?.Split(',')[0].Trim();
        }

        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            return realIp.FirstOrDefault();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks
    /// </summary>
    private static bool SecureEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}
