using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.Correspondence.Common.Caching;

public class HybridCacheWrapper : IHybridCacheWrapper
{
    private readonly HybridCache _hybridCache;

    public HybridCacheWrapper(HybridCache hybridCache)
    {
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
    }

    public Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return _hybridCache.GetOrCreateAsync(key, factory, options, tags, cancellationToken).AsTask();
    }
    
    public Task SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return _hybridCache.SetAsync(key, value, options, tags, cancellationToken).AsTask();
    }
    
    public Task SetAsync(
        string key,
        byte[] value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return _hybridCache.SetAsync(key, value, options, tags, cancellationToken).AsTask();
    }
    
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return _hybridCache.RemoveAsync(key, cancellationToken).AsTask();
    }
}