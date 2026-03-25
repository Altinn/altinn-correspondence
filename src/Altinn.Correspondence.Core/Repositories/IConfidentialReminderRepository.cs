using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IConfidentialReminderRepository
    {
        Task<Guid> AddConfidentialReminder(ConfidentialReminderEntity reminder, CancellationToken cancellationToken);
        Task RemoveConfidentialReminderByCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken);
        Task<int> NumberOfRemindersForRecipient(string recipient, CancellationToken cancellationToken);
        Task<bool> CorrespondenceHasReminder(Guid correspondenceId, CancellationToken cancellationToken);
        Task<Guid?> GetDialogIdOfReminderForRecipient(string recipient, CancellationToken cancellationToken);
    }
}