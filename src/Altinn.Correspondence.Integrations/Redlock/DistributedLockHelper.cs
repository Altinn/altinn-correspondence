using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using Altinn.Correspondence.Core.Options;

namespace Altinn.Correspondence.Integrations.Redlock
{
    public class DistributedLockHelper : IDisposable
    {
        private readonly RedLockFactory _lockFactory;
        private readonly ILogger<DistributedLockHelper> _logger;
        public const int DefaultRetryCount = 5;
        public const int DefaultRetryDelayMs = 500;
        private const string LockKeyPrefix = "lock:";

        public DistributedLockHelper(IOptions<GeneralSettings> generalSettings, ILogger<DistributedLockHelper> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var redisConnectionString = generalSettings?.Value?.RedisConnectionString;
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                redisConnectionString = Environment.GetEnvironmentVariable("GeneralSettings__RedisConnectionString");
            }
            
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                throw new InvalidOperationException("Redis connection string not found. Please add 'RedisConnectionString' to GeneralSettings in your configuration or set the 'GeneralSettings__RedisConnectionString' environment variable.");
            }

            var connectionMultiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
            var multiplexers = new List<RedLockMultiplexer> { new RedLockMultiplexer(connectionMultiplexer) };
            _lockFactory = RedLockFactory.Create(multiplexers);
        }

        /// <summary>
        /// Acquires a distributed lock and executes the action if successful.
        /// The lock is released automatically when the action is completed.
        /// </summary>
        /// <param name="lockKey">The unique key for the lock</param>
        /// <param name="action">The action to execute if lock is acquired</param>
        /// <param name="retryCount">Number of retries if lock acquisition fails (default: 3)</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds (default: 500)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if lock was acquired and action executed; false otherwise</returns>
        public async Task<bool> ExecuteWithLockAsync(
            string lockKey, 
            Func<CancellationToken, Task> action,
            int retryCount = DefaultRetryCount,
            int retryDelayMs = DefaultRetryDelayMs,
            CancellationToken cancellationToken = default)
        {
            var prefixedKey = $"{LockKeyPrefix}{lockKey}";
            var expiryTime = TimeSpan.FromSeconds(30);
            var waitTime = TimeSpan.FromMilliseconds(retryCount * retryDelayMs);
            var retryTime = TimeSpan.FromMilliseconds(retryDelayMs);
            
            using var redLock = await _lockFactory.CreateLockAsync(
                resource: prefixedKey,
                expiryTime: expiryTime,
                waitTime: waitTime,
                retryTime: retryTime,
                cancellationToken: cancellationToken);
            
            if (!redLock.IsAcquired)
            {
                _logger.LogWarning("Could not acquire lock for key {lockKey} after {retryCount} attempts", 
                    lockKey, retryCount);
                return false;
            }

            try
            {
                _logger.LogDebug("Lock acquired for key {lockKey}", lockKey);
                await action(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing action with lock for key {lockKey}", lockKey);
                throw;
            }
        }

        /// <summary>
        /// Attempts to acquire a distributed lock to exequte the action, first checking a condition before each attempt.
        /// If the condition returns true (indicating no work is needed), the method returns early without executing the action.
        /// The lock is released automatically when the action is completed.
        /// </summary>
        /// <param name="lockKey">The unique key for the lock</param>
        /// <param name="shouldSkipCheck">Function that returns true if the operation should be skipped</param>
        /// <param name="action">The action to execute if lock is acquired and condition is false</param>
        /// <param name="retryCount">Number of retries if lock acquisition fails</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A tuple: (wasSkipped, lockAcquired) where wasSkipped indicates if the operation was skipped due to the condition, and lockAcquired indicates if the lock was acquired</returns>
        public async Task<(bool wasSkipped, bool lockAcquired)> ExecuteWithConditionalLockAsync(
            string lockKey,
            Func<CancellationToken, Task<bool>> shouldSkipCheck,
            Func<CancellationToken, Task> action,
            int retryCount = DefaultRetryCount,
            int retryDelayMs = DefaultRetryDelayMs,
            CancellationToken cancellationToken = default)
        {
            // Initial check if we should skip the operation
            bool shouldSkip = await shouldSkipCheck(cancellationToken);
            if (shouldSkip)
            {
                _logger.LogDebug("Skipping lock acquisition for key {lockKey} based on condition check", lockKey);
                return (wasSkipped: true, lockAcquired: false);
            }
            
            var prefixedKey = $"{LockKeyPrefix}{lockKey}";
            var expiryTime = TimeSpan.FromSeconds(30);
            
            // Custom implementation of retry logic to check condition before each attempt
            for (int attempt = 0; attempt <= retryCount; attempt++)
            {
                // Skip first delay
                if (attempt > 0)
                {
                    // Check condition again before retry
                    shouldSkip = await shouldSkipCheck(cancellationToken);
                    if (shouldSkip)
                    {
                        _logger.LogDebug("Skipping lock acquisition for key {lockKey} based on condition check before retry #{attempt}", lockKey, attempt);
                        return (wasSkipped: true, lockAcquired: false);
                    }
                    
                    await Task.Delay(retryDelayMs, cancellationToken);
                }
                
                using var redLock = await _lockFactory.CreateLockAsync(
                    resource: prefixedKey,
                    expiryTime: expiryTime,
                    waitTime: TimeSpan.Zero, // Don't wait, we handle retry logic ourselves
                    retryTime: TimeSpan.Zero, // Don't retry, we handle retry logic ourselves
                    cancellationToken: cancellationToken);
                
                if (redLock.IsAcquired)
                {
                    try
                    {
                        _logger.LogDebug("Lock acquired for key {lockKey} on attempt #{attempt}", lockKey, attempt);
                        await action(cancellationToken);
                        return (wasSkipped: false, lockAcquired: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing action with lock for key {lockKey}", lockKey);
                        throw;
                    }
                }
                
                _logger.LogDebug("Failed to acquire lock for key {lockKey} on attempt #{attempt}, will retry", lockKey, attempt);
            }
            
            _logger.LogWarning("Could not acquire lock for key {lockKey} after {retryCount} attempts", lockKey, retryCount);
            return (wasSkipped: false, lockAcquired: false);
        }

        public void Dispose()
        {
            _lockFactory.Dispose();
            GC.SuppressFinalize(this);
        }
    }
} 