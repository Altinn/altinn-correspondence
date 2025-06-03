using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using Altinn.Correspondence.Core.Options;

namespace Altinn.Correspondence.Integrations.Redlock
{
    public class DistributedLockHelper : IDistributedLockHelper
    {
        private readonly RedLockFactory _lockFactory;
        private readonly ILogger<DistributedLockHelper> _logger;
        public const int DefaultRetryCount = 5;
        public const int DefaultRetryDelayMs = 500;
        public const int DefaultLockExpirySeconds = 30;
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
                throw new InvalidOperationException("Redis connection string not found");
            }

            try
            {
                var connectionMultiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
                var multiplexers = new List<RedLockMultiplexer> { new RedLockMultiplexer(connectionMultiplexer) };
                _lockFactory = RedLockFactory.Create(multiplexers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis using the provided connection string");
                throw new InvalidOperationException("Failed to establish Redis connection. See inner exception for details.", ex);
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
            int lockExpirySeconds = DefaultLockExpirySeconds,
            CancellationToken cancellationToken = default)
        {
            var prefixedKey = $"{LockKeyPrefix}{lockKey}";
            var expiryTime = TimeSpan.FromSeconds(lockExpirySeconds);
            
            for (int attempt = 0; attempt <= retryCount; attempt++)
            {
                if (await shouldSkipCheck(cancellationToken))
                {
                    return (wasSkipped: true, lockAcquired: false);
                }

                using var redLock = await _lockFactory.CreateLockAsync(
                    resource: prefixedKey,
                    expiryTime: expiryTime,
                    waitTime: TimeSpan.Zero,
                    retryTime: TimeSpan.Zero,
                    cancellationToken: cancellationToken);

                if (redLock.IsAcquired)
                {
                    if (await shouldSkipCheck(cancellationToken))
                    {
                        return (wasSkipped: true, lockAcquired: true);
                    }
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

                if (attempt != retryCount)
                {
                    _logger.LogDebug("Failed to acquire lock for key {lockKey} on attempt #{attempt}, will retry", lockKey, attempt);
                    await Task.Delay(retryDelayMs, cancellationToken);
                }
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