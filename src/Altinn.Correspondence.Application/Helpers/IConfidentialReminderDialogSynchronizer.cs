namespace Altinn.Correspondence.Application.Helpers;

/// <summary>
/// Serializes confidential-reminder dialog create vs final-reminder cleanup per recipient.
/// </summary>
public interface IConfidentialReminderDialogSynchronizer
{
    Task<T> ExecuteForRecipientAsync<T>(
        string recipientUrn,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteForRecipientAsync(
        string recipientUrn,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
