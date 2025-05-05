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
        public const int DefaultRetryCount = 3;
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

        public void Dispose()
        {
            _lockFactory.Dispose();
            GC.SuppressFinalize(this);
        }
    }
} 