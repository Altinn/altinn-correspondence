using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Correspondence.Common.Helpers
{
    public static class CacheHelpers
    {
        private static async Task StoreObjectInCacheAsync<T>(string key, T value, IDistributedCache cache, DistributedCacheEntryOptions cacheOptions, CancellationToken cancellationToken = default)
        {
            string serializedDataString = JsonSerializer.Serialize(value);
            await cache.SetStringAsync(key, serializedDataString, cacheOptions, cancellationToken);
        }

        private static async Task<T?> GetObjectFromCacheAsync<T>(string key, IDistributedCache cache, CancellationToken cancellationToken = default)
        {
            string? cachedDataString = await cache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrEmpty(cachedDataString))
            {
                return JsonSerializer.Deserialize<T?>(cachedDataString);
            }
            return default;
        }
    }
}