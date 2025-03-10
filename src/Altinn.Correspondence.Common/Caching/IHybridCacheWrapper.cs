using Microsoft.Extensions.Caching.Hybrid; // Ensure correct namespace for HybridCacheEntryOptions

namespace Altinn.Correspondence.Common.Caching;

public interface IHybridCacheWrapper
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);
    Task SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);
    
    // New explicit SetAsync for byte arrays
    Task SetAsync(
        string key,
        byte[] value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);
    
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}