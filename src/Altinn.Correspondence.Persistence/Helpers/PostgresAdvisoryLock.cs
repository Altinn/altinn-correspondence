using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Helpers;

public static class PostgresAdvisoryLock
{
    /// <summary>
    /// Acquires a transaction-scoped advisory lock that is released when the current DB transaction ends.
    /// Use a stable name (e.g. recipient URN) so create and cleanup for the same party serialize.
    /// No-op for non-PostgreSQL providers (e.g. in-memory test contexts).
    /// </summary>
    public static async Task AcquireTransactionLockAsync(
        ApplicationDbContext dbContext,
        string lockName,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        var lockKey = DeriveLockKey(lockName);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);
    }

    private static long DeriveLockKey(string lockName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(lockName));
        return BitConverter.ToInt64(hash, 0);
    }
}
