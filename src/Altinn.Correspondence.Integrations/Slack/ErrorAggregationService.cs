using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Integrations.Slack;

public class ErrorAggregationService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<ErrorAggregationService> _logger;
    private readonly TimeSpan _aggregationWindow = TimeSpan.FromMinutes(5);
    private const string ActiveErrorsKey = "active_errors";

    public ErrorAggregationService(
        IDistributedCache cache,
        ILogger<ErrorAggregationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<(bool ShouldSend, int Count)> ShouldSendNotification(string message)
    {
        var key = $"error:{message}";
        var count = await GetErrorCount(key);
        
        // Increment count
        count++;
        await SetErrorCount(key, count);

        // Add to active errors if this is the first occurrence
        if (count == 1)
        {
            await AddToActiveErrors(message);
            _logger.LogInformation("First occurrence of error: {Message}", message);
            return (true, count);
        }

        _logger.LogInformation("Error count is {Count} for message: {Message}", count, message);
        return (false, count);
    }

    public async Task<List<(string Message, int Count)>> GetActiveErrors()
    {
        var activeErrorsJson = await _cache.GetStringAsync(ActiveErrorsKey);
        if (string.IsNullOrEmpty(activeErrorsJson))
        {
            return new List<(string Message, int Count)>();
        }

        var activeErrors = JsonSerializer.Deserialize<List<string>>(activeErrorsJson);
        var result = new List<(string Message, int Count)>();

        foreach (var message in activeErrors)
        {
            var count = await GetErrorCount($"error:{message}");
            result.Add((message, count));
        }

        return result;
    }

    public async Task ClearActiveErrors()
    {
        var activeErrors = await GetActiveErrors();
        foreach (var (message, _) in activeErrors)
        {
            await _cache.RemoveAsync($"error:{message}");
        }
        await _cache.RemoveAsync(ActiveErrorsKey);
    }

    private async Task AddToActiveErrors(string message)
    {
        var activeErrorsJson = await _cache.GetStringAsync(ActiveErrorsKey);
        var activeErrors = string.IsNullOrEmpty(activeErrorsJson) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(activeErrorsJson);

        if (!activeErrors.Contains(message))
        {
            activeErrors.Add(message);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _aggregationWindow
            };
            await _cache.SetStringAsync(ActiveErrorsKey, JsonSerializer.Serialize(activeErrors), options);
        }
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