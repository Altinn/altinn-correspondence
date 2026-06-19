using Altinn.Correspondence.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace Altinn.Correspondence.Application.Helpers;

public static class DatabaseTransactionHelper
{
    /// <summary>
    /// Runs the operation inside the EF execution strategy with an ambient transaction that commits on success.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        ApplicationDbContext dbContext,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(dbContext, async ct =>
        {
            using var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted,
                    Timeout = TimeSpan.FromSeconds(30)
                },
                TransactionScopeAsyncFlowOption.Enabled);
            var result = await operation(ct);
            transaction.Complete();
            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Runs the operation inside the EF execution strategy only. The caller owns transaction boundaries and Commit/Complete.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        ApplicationDbContext dbContext,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => operation(cancellationToken));
    }
}
