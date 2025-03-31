using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Altinn.Correspondence.Integrations.Slack;

public class ErrorAggregationService
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _aggregationWindow = TimeSpan.FromMinutes(5);
    private const string KeyPrefix = "error_count_";

    public ErrorAggregationService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> ShouldSendNotification(string message)
    {
        var key = KeyPrefix + message;
        var count = await GetErrorCount(key);
        
        // Increment count
        count++;
        await SetErrorCount(key, count);

        // If this is the first error in the window, send notification
        if (count == 1)
        {
            return true;
        }

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