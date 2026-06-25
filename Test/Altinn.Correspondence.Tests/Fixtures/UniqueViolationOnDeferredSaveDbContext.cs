using Altinn.Correspondence.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Altinn.Correspondence.Tests.Fixtures;

/// <summary>
/// Simulates a concurrent idempotency insert losing the race on the batched flush inside
/// <see cref="Altinn.Correspondence.Application.Helpers.DatabaseTransactionHelper.ExecuteAsync"/>.
/// </summary>
public sealed class UniqueViolationOnDeferredSaveDbContext(
    DbContextOptions<ApplicationDbContext> options,
    int uniqueViolationOnDeferredSaveAttempt = 1)
    : TestApplicationDbContext(options)
{
    private int _deferredSaveAttempts;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (DeferSaveChanges)
        {
            _deferredSaveAttempts++;
            if (_deferredSaveAttempts == uniqueViolationOnDeferredSaveAttempt)
            {
                throw new DbUpdateException(
                    "duplicate key",
                    new PostgresException(
                        "duplicate key value violates unique constraint",
                        "ERROR",
                        "ERROR",
                        "23505"));
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
