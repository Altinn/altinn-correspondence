using System.Text.Json;
using Altinn.Correspondence.Common.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;


namespace Altinn.Correspondence.Common.Helpers
{
    public static class CacheHelpers
    {
        public static async Task StoreObjectInCacheAsync<T>(string key, T value, IHybridCacheWrapper cache, HybridCacheEntryOptions cacheOptions, CancellationToken cancellationToken = default)
        {
            string serializedDataString = JsonSerializer.Serialize(value);
            await cache.SetAsync(
                key, 
                serializedDataString,
                cacheOptions, 
                null,
                cancellationToken);
        }

        public static async Task<T?> GetObjectFromCacheAsync<T>(string key, IHybridCacheWrapper cache, CancellationToken cancellationToken = default)
        {
            string cachedDataString = await cache.GetOrCreateAsync(
                key,
                async cancellationToken => await Task.FromResult<string>(null!), // Provide default value if missing
                new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
                null, // No tags
                cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedDataString))
            {
                return JsonSerializer.Deserialize<T?>(cachedDataString);
            }
            return default;
        }
    }
}