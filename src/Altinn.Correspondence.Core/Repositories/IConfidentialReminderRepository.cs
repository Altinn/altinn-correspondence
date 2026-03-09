using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IConfidentialReminderRepository
    {
        Task<Guid> AddConfidentialReminder(ConfidentialReminderEntity reminder, CancellationToken cancellationToken);
        Task RemoveConfidentialReminder(Guid reminderId, CancellationToken cancellationToken);
        Task<int> NumberOfRemindersForRecipient(string recipient, CancellationToken cancellationToken);
        Task<bool> CorrespondenceHasReminder(Guid correspondenceId, CancellationToken cancellationToken);
        Task<string> GetDialogIdOfReminderForRecipient(string recipient, CancellationToken cancellationToken);
    }
}