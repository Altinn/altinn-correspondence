namespace Altinn.Correspondence.Integrations.Redlock
{
    public interface IDistributedLockHelper : IDisposable
    {
        /// <summary>
        /// Attempts to acquire a distributed lock to execute the action, first checking a condition before each attempt.
        /// If the condition returns true (indicating no work is needed), the method returns early without executing the action.
        /// The lock is released automatically when the action is completed.
        /// </summary>
        /// <param name="lockKey">The unique key for the lock</param>
        /// <param name="shouldSkipCheck">Function that returns true if the operation should be skipped</param>
        /// <param name="action">The action to execute if lock is acquired and condition is false</param>
        /// <param name="retryCount">Number of retries if lock acquisition fails</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds</param>
        /// <param name="lockExpirySeconds">Lock expiry time in seconds</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A tuple: (wasSkipped, lockAcquired) where wasSkipped indicates if the operation was skipped due to the condition, and lockAcquired indicates if the lock was acquired</returns>
        Task<(bool wasSkipped, bool lockAcquired)> ExecuteWithConditionalLockAsync(
            string lockKey,
            Func<CancellationToken, Task<bool>> shouldSkipCheck,
            Func<CancellationToken, Task> action,
            int retryCount,
            int retryDelayMs,
            int lockExpirySeconds,
            CancellationToken cancellationToken);
    }
} 