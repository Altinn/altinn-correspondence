using System.Transactions;

using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;
using OneOf;
using Polly;
using Polly.Retry;

namespace Altinn.Correspondence.Application.Helpers;
public static class TransactionWithRetriesPolicy
{
    public static async Task<OneOf<T, Error>> Execute<T>(
        Func<CancellationToken, Task<OneOf<T, Error>>> operation,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var result = await RetryPolicy(logger).ExecuteAndCaptureAsync<OneOf<T, Error>>(async (cancellationToken) =>
        {
            using var transaction = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions()
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TransactionManager.MaximumTimeout
            }, TransactionScopeAsyncFlowOption.Enabled);
            var result = await operation(cancellationToken);
            transaction.Complete();
            return result;
        }, cancellationToken);
        if (result.Outcome == OutcomeType.Failure)
        {
            logger.LogError("Exception during retries: {message}\n{stackTrace}", result.FinalException.Message, result.FinalException.StackTrace);
            throw result.FinalException;
        }
        return result.Result;
    }

    public static AsyncRetryPolicy RetryPolicy(ILogger logger) => Policy
        .Handle<TransactionAbortedException>()
        .Or<PostgresException>()
        .Or<BackgroundJobClientException>()
        .Or<PostgreSqlDistributedLockException>()
        .Or<DbUpdateConcurrencyException>()
        .WaitAndRetryAsync(
            10,
            retryAttempt => TimeSpan.FromMilliseconds(10),
            (exception, timeSpan, retryCount, context) =>
            {
                logger.LogWarning($"Attempt {retryCount} failed with exception {exception.Message}. Retrying in {timeSpan.Milliseconds} milliseconds.");
            }
        );
}
