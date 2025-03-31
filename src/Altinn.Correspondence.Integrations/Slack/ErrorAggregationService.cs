using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Integrations.Slack;

public class ErrorAggregationService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<ErrorAggregationService> _logger;
    private readonly TimeSpan _aggregationWindow = TimeSpan.FromMinutes(5);

    public ErrorAggregationService(
        IDistributedCache cache,
        ILogger<ErrorAggregationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> ShouldSendNotification(string message)
    {
        var key = $"error:{message}";
        var count = await GetErrorCount(key);
        
        // Increment count
        count++;
        await SetErrorCount(key, count);

        // Send notification on first error
        if (count == 1)
        {
            _logger.LogInformation("First occurrence of error: {Message}", message);
            return true;
        }

        // Send summary when count is a multiple of 5
        if (count % 5 == 0)
        {
            _logger.LogInformation("Error count reached {Count} for message: {Message}", count, message);
            return true;
        }

        _logger.LogInformation("Error count is {Count} for message: {Message}", count, message);
        return false;
    }

    private async Task<int> GetErrorCount(string key)
    {
        var value = await _cache.GetStringAsync(key);
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        return JsonSerializer.Deserialize<int>(value);
    }

    private async Task SetErrorCount(string key, int count)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _aggregationWindow
        };

        var value = JsonSerializer.Serialize(count);
        await _cache.SetStringAsync(key, value, options);
    }
} 