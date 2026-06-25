using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace Altinn.Correspondence.Application.Helpers;

public static class DatabaseTransactionHelper
{
    /// <summary>
    /// Optional behaviour when the batched flush hits a PostgreSQL unique-constraint violation.
    /// The ambient transaction is rolled back before the handler result is returned.
    /// </summary>
    public readonly record struct TransactionOptions<T>(Func<DbUpdateException, T>? OnUniqueViolation = null);

    /// <summary>
    /// Idempotency helpers for use with staged writes inside <see cref="ExecuteAsync"/>.
    /// Typical flow: <see cref="CheckAsync{T}"/> before validation-heavy work,
    /// <see cref="StageAsync"/> inside the transaction, and <see cref="OnDuplicate{T}"/>
    /// on <see cref="ExecuteAsync"/> to handle concurrent inserts on flush.
    /// </summary>
    public static class Idempotency
    {
        /// <summary>
        /// Result of a pre-transaction idempotency check.
        /// </summary>
        public readonly record struct CheckResult<T>(bool IsDuplicate, T? DuplicateResult = default)
        {
            public static CheckResult<T> Proceed => default;
            public static CheckResult<T> Duplicate(T result) => new(true, result);
        }

        public static async Task<bool> ExistsAsync(
            IIdempotencyKeyRepository repository,
            Guid id,
            CancellationToken cancellationToken)
        {
            return await repository.GetByIdAsync(id, cancellationToken) is not null;
        }

        /// <summary>
        /// Pre-transaction duplicate check for business/validation logic.
        /// Returns <see cref="CheckResult{T}.Duplicate"/> when the key already exists.
        /// </summary>
        public static async Task<CheckResult<T>> CheckAsync<T>(
            IIdempotencyKeyRepository repository,
            Guid id,
            Func<T> onDuplicate,
            CancellationToken cancellationToken)
        {
            if (await ExistsAsync(repository, id, cancellationToken))
            {
                return CheckResult<T>.Duplicate(onDuplicate());
            }

            return CheckResult<T>.Proceed;
        }

        public static Task StageAsync(
            IIdempotencyKeyRepository repository,
            IdempotencyKeyEntity key,
            CancellationToken cancellationToken)
        {
            return repository.CreateAsync(key, cancellationToken);
        }

        /// <summary>
        /// Maps a unique-constraint violation on the batched flush to a duplicate outcome.
        /// </summary>
        public static TransactionOptions<T> OnDuplicate<T>(Func<T> onDuplicate)
        {
            return new TransactionOptions<T>(_ => onDuplicate());
        }
    }

    /// <summary>
    /// Runs the operation inside the EF execution strategy with an ambient transaction that commits on success.
    /// Stages EF changes during the operation and flushes them in a single SaveChanges before commit.
    /// </summary>
    public static Task<T> ExecuteAsync<T>(
        ApplicationDbContext dbContext,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(dbContext, operation, cancellationToken, default(TransactionOptions<T>));
    }

    /// <summary>
    /// Runs the operation inside the EF execution strategy with an ambient transaction that commits on success.
    /// Stages EF changes during the operation and flushes them in a single SaveChanges before commit.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        ApplicationDbContext dbContext,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        TransactionOptions<T> options)
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
            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsPostgresUniqueViolation())
            {
                if (options.OnUniqueViolation is { } onUniqueViolation)
                {
                    return onUniqueViolation(ex);
                }

                throw;
            }

            transaction.Complete();
            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Runs the operation inside the EF execution strategy only. The caller owns transaction boundaries and Commit/Complete.
    /// Clears the change tracker before each retry attempt.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        ApplicationDbContext dbContext,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            dbContext.ChangeTracker.Clear();
            return await operation(ct);
        }, cancellationToken);
    }
}
