using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.Correspondence.Common.Helpers
{
    public static class CacheHelpers
    {
        public static async Task StoreObjectInCacheAsync<T>(string key, T value, HybridCache cache, HybridCacheEntryOptions cacheOptions, CancellationToken cancellationToken = default)
        {
            string serializedDataString = JsonSerializer.Serialize(value);
            await cache.GetOrCreateAsync<string>(
                key,
                async token => serializedDataString, // Correct: Provide a factory function
                cacheOptions, // HybridCacheEntryOptions
                null, // Tags (optional, can be null)
                cancellationToken); // CancellationToken
        }
        // var token = await _cache.GetOrCreateAsync(sessionId, async entry =>
        // {
        //     var cacheEntryOptions = new DistributedCacheEntryOptions
        //     {
        //         AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        //     };
        //     return await Task.FromResult<string>(null);
        // });
        public static async Task<T?> GetObjectFromCacheAsync<T>(string key, HybridCache cache, CancellationToken cancellationToken = default)
        {
            string? cachedDataString = await cache.GetOrCreateAsync<string>(
    key,
    async token =>
    {
        // This function is called if the key is not found in the cache.
        // Replace the following line with your data retrieval logic.
        return await Task.FromResult("Fetched Data");
    },
    new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5), // Sets expiration for the distributed cache
        LocalCacheExpiration = TimeSpan.FromMinutes(2) // Sets expiration for the in-memory cache
    },
    null,
    cancellationToken);


            if (!string.IsNullOrEmpty(cachedDataString))
            {
                return JsonSerializer.Deserialize<T?>(cachedDataString);
            }
            return default;
        }
    }
}