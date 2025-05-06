using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using System.Collections.Concurrent;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Options;

namespace Altinn.Correspondence.Integrations.Redlock
{
    /// <summary>
    /// Hybrid distributed lock helper that uses both in-memory cache and Redis for distributed locking.
    /// </summary>
    public class HybridDistributedLockHelper : IDisposable
    {
        private readonly RedLockFactory _lockFactory;
        private readonly ILogger<HybridDistributedLockHelper> _logger;
        private readonly IHybridCacheWrapper _cache;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _localLocks = new();
        private readonly TimeSpan _lockExpiry = TimeSpan.FromSeconds(30);
        private readonly HybridCacheEntryOptions _cacheOptions;
        
        public const int DefaultRetryCount = 2;
        public const int DefaultRetryDelayMs = 100;
        private const string LockKeyPrefix = "lock:";

        public HybridDistributedLockHelper(
            IOptions<GeneralSettings> generalSettings, 
            ILogger<HybridDistributedLockHelper> logger,
            IHybridCacheWrapper cache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _cacheOptions = new HybridCacheEntryOptions
            {
                Expiration = _lockExpiry
            };

            var redisConnectionString = generalSettings?.Value?.RedisConnectionString;
            
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                throw new InvalidOperationException("Redis connection string not found. 'RedisConnectionString' missing in GeneralSettings inthe configuration");
            }

            var connectionMultiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
            var multiplexers = new List<RedLockMultiplexer> { new RedLockMultiplexer(connectionMultiplexer) };
            _lockFactory = RedLockFactory.Create(multiplexers);
        }
        
        /// <summary>
        /// Acquires a hybrid distributed lock to execute an action, first checking a condition before each attempt.
        /// Uses hybrid cache first when possible, falling back to Redis for distributed scenarios.
        /// </summary>
        /// <param name="lockKey">The unique key for the lock</param>
        /// <param name="shouldSkipCheck">Function that returns true if the operation should be skipped</param>
        /// <param name="action">The action to execute if lock is acquired and condition is false</param>
        /// <param name="retryCount">Number of retries if lock acquisition fails</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A tuple: (wasSkipped, lockAcquired) where wasSkipped indicates if the operation was skipped due to the condition, and lockAcquired indicates if the lock was acquired</returns>
        public async Task<(bool wasSkipped, bool lockAcquired)> ExecuteWithConditionalHybridLockAsync(
            string lockKey,
            Func<CancellationToken, Task<bool>> shouldSkipCheck,
            Func<CancellationToken, Task> action,
            int retryCount = DefaultRetryCount,
            int retryDelayMs = DefaultRetryDelayMs,
            CancellationToken cancellationToken = default)
        {
            bool shouldSkip = await shouldSkipCheck(cancellationToken);
            if (shouldSkip)
            {
                _logger.LogDebug("Skipping lock acquisition for key {lockKey} based on condition check", lockKey);
                return (wasSkipped: true, lockAcquired: false);
            }
            
            var cacheKey = $"{LockKeyPrefix}{lockKey}";
            
            if (await IsLockedAsync(cacheKey, cancellationToken))
            {
                _logger.LogDebug("Resource {lockKey} is already locked", lockKey);
                
                shouldSkip = await shouldSkipCheck(cancellationToken);
                if (shouldSkip)
                {
                    return (wasSkipped: true, lockAcquired: false);
                }
                
                return (wasSkipped: false, lockAcquired: false);
            }
            
            var localSemaphore = _localLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            
            try
            {
                var hasLocalLock = await localSemaphore.WaitAsync(0, cancellationToken);
                if (!hasLocalLock)
                {
                    shouldSkip = await shouldSkipCheck(cancellationToken);
                    if (shouldSkip)
                    {
                        return (wasSkipped: true, lockAcquired: false);
                    }
                    
                    _logger.LogDebug("Could not acquire local semaphore for key {lockKey}, resource busy locally", lockKey);
                    return (wasSkipped: false, lockAcquired: false);
                }
                
                try
                {
                    await SetLockAsync(cacheKey, cancellationToken);
                    
                    for (int attempt = 0; attempt <= retryCount; attempt++)
                    {
                        if (attempt > 0)
                        {
                            shouldSkip = await shouldSkipCheck(cancellationToken);
                            if (shouldSkip)
                            {
                                _logger.LogDebug("Skipping lock acquisition for key {lockKey} based on condition check before retry #{attempt}", lockKey, attempt);
                                return (wasSkipped: true, lockAcquired: false);
                            }
                            
                            await Task.Delay(retryDelayMs, cancellationToken);
                        }
                        
                        using var redLock = await _lockFactory.CreateLockAsync(
                            resource: cacheKey,
                            expiryTime: _lockExpiry,
                            waitTime: TimeSpan.Zero,
                            retryTime: TimeSpan.Zero,
                            cancellationToken: cancellationToken);
                        
                        if (redLock.IsAcquired)
                        {
                            try
                            {
                                await action(cancellationToken);
                                return (wasSkipped: false, lockAcquired: true);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error executing action with lock for key {lockKey}", lockKey);
                                throw;
                            }
                        }
                    }
                    
                    _logger.LogWarning("Could not acquire lock for key {lockKey} after {retryCount} attempts", lockKey, retryCount);
                    return (wasSkipped: false, lockAcquired: false);
                }
                finally
                {
                    await RemoveLockAsync(cacheKey, cancellationToken);
                    localSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExecuteWithConditionalHybridLockAsync for key {lockKey}", lockKey);
                throw;
            }
        }

        private async Task<bool> IsLockedAsync(string cacheKey, CancellationToken cancellationToken)
        {
            var result = await _cache.GetAsync<bool>(cacheKey, _cacheOptions, null, cancellationToken);
            return result;
        }
        
        private async Task SetLockAsync(string cacheKey, CancellationToken cancellationToken)
        {
            await _cache.SetAsync(cacheKey, true, _cacheOptions, null, cancellationToken);
        }
        
        private async Task RemoveLockAsync(string cacheKey, CancellationToken cancellationToken)
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }

        public void Dispose()
        {
            _lockFactory.Dispose();
            
            foreach (var semaphore in _localLocks.Values)
            {
                semaphore.Dispose();
            }
            
            GC.SuppressFinalize(this);
        }
    }
} 