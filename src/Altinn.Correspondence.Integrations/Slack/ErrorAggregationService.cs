using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Integrations.Slack;

public class ErrorAggregationService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<ErrorAggregationService> _logger;
    private readonly TimeSpan _aggregationWindow = TimeSpan.FromMinutes(3);
    private const string ActiveErrorsKey = "active_errors";

    public ErrorAggregationService(
        IDistributedCache cache,
        ILogger<ErrorAggregationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<(bool ShouldSend, int Count)> ShouldSendNotification(string message, Exception exception, string source)
    {
        // Hent feiltypen fra unntaket
        var errorType = GetErrorType(exception);
        var key = $"error:{errorType}:{source}:{message}";
        var count = await GetErrorCount(key);
        
        // Increment count
        count++;
        await SetErrorCount(key, count);

        // Add to active errors if this is the first occurrence
        if (count == 1)
        {
            await AddToActiveErrors(errorType, source, message);
            _logger.LogInformation("First occurrence of error type {ErrorType} in {Source}: {Message}", errorType, source, message);
            return (true, count);
        }

        _logger.LogInformation("Error count is {Count} for error type {ErrorType} in {Source}: {Message}", count, errorType, source, message);
        return (false, count);
    }

    public async Task<List<(string ErrorType, string Source, string Message, int Count)>> GetActiveErrors()
    {
        var activeErrorsJson = await _cache.GetStringAsync(ActiveErrorsKey);
        if (string.IsNullOrEmpty(activeErrorsJson))
        {
            return new List<(string ErrorType, string Source, string Message, int Count)>();
        }

        var activeErrors = JsonSerializer.Deserialize<List<(string ErrorType, string Source, string Message)>>(activeErrorsJson);
        var result = new List<(string ErrorType, string Source, string Message, int Count)>();

        foreach (var (errorType, source, message) in activeErrors)
        {
            var count = await GetErrorCount($"error:{errorType}:{source}:{message}");
            result.Add((errorType, source, message, count));
        }

        return result;
    }

    public async Task ClearActiveErrors()
    {
        var activeErrors = await GetActiveErrors();
        foreach (var (errorType, source, message, _) in activeErrors)
        {
            await _cache.RemoveAsync($"error:{errorType}:{source}:{message}");
        }
        await _cache.RemoveAsync(ActiveErrorsKey);
    }

    private async Task AddToActiveErrors(string errorType, string source, string message)
    {
        var activeErrorsJson = await _cache.GetStringAsync(ActiveErrorsKey);
        var activeErrors = string.IsNullOrEmpty(activeErrorsJson) 
            ? new List<(string ErrorType, string Source, string Message)>() 
            : JsonSerializer.Deserialize<List<(string ErrorType, string Source, string Message)>>(activeErrorsJson);

        if (!activeErrors.Contains((errorType, source, message)))
        {
            activeErrors.Add((errorType, source, message));
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

    private string GetErrorType(Exception exception)
    {
        return exception switch
        {
            NotImplementedException => "NotImplemented",
            ArgumentException => "Argument",
            InvalidOperationException => "InvalidOperation",
            _ => "Unknown"
        };
    }
} 