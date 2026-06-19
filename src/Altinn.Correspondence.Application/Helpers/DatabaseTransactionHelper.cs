using Altinn.Correspondence.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace Altinn.Correspondence.Application.Helpers;

public static class DatabaseTransactionHelper
{
    public static async Task<T> ExecuteAsync<T>(
        ApplicationDbContext dbContext,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted,
                    Timeout = TimeSpan.FromSeconds(30)
                },
                TransactionScopeAsyncFlowOption.Enabled);
            var result = await operation(cancellationToken);
            transaction.Complete();
            return result;
        });
    }
}
