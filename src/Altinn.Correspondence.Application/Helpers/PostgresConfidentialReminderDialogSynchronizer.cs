using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;

namespace Altinn.Correspondence.Application.Helpers;

public class PostgresConfidentialReminderDialogSynchronizer(ApplicationDbContext dbContext) : IConfidentialReminderDialogSynchronizer
{
    public Task<T> ExecuteForRecipientAsync<T>(
        string recipientUrn,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return DatabaseTransactionHelper.ExecuteAsync(dbContext, async ct =>
        {
            await PostgresAdvisoryLock.AcquireTransactionLockAsync(
                dbContext,
                $"confidential-reminder-dialog:{recipientUrn}",
                ct);
            return await operation(ct);
        }, cancellationToken);
    }

    public Task ExecuteForRecipientAsync(
        string recipientUrn,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteForRecipientAsync(recipientUrn, async ct =>
        {
            await operation(ct);
            return true;
        }, cancellationToken);
}
