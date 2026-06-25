using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using System.Transactions;

namespace Altinn.Correspondence.Persistence.Helpers;

public class CorrespondenceNpgsqlRetryingExecutionStrategy : NpgsqlRetryingExecutionStrategy
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    public CorrespondenceNpgsqlRetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies, MaxRetries, MaxRetryDelay, ["40001", "40P01"])
    {
    }

    protected override bool ShouldRetryOn(Exception? exception)
    {
        if (exception is BackgroundJobClientException or PostgreSqlDistributedLockException)
        {
            return false;
        }

        return base.ShouldRetryOn(exception)
            || exception is TransactionAbortedException
            || exception is DbUpdateConcurrencyException;
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
