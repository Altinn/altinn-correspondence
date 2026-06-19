using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using System.Transactions;

namespace Altinn.Correspondence.Persistence.Helpers;

public class CorrespondenceNpgsqlRetryingExecutionStrategy : NpgsqlRetryingExecutionStrategy
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    public CorrespondenceNpgsqlRetryingExecutionStrategy(ExecutionStrategyDependencies dependencies, int maxRetryCount = 5)
        : base(dependencies, maxRetryCount, MaxRetryDelay, ["40001", "40P01"])
    {
    }

    protected override bool ShouldRetryOn(Exception? exception)
    {
        return base.ShouldRetryOn(exception)
            || exception is TransactionAbortedException
            || exception is DbUpdateConcurrencyException
            || exception is BackgroundJobClientException
            || exception is PostgreSqlDistributedLockException;
    }

    protected override TimeSpan? GetNextDelay(Exception lastException)
    {
        var retryCount = ExceptionsEncountered.Count;
        if (retryCount > MaxRetryCount)
        {
            return null;
        }

        return TimeSpan.FromMilliseconds(Math.Pow(2, retryCount) * 50);
    }
}
