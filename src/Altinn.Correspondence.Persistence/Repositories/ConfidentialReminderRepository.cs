using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories;

public class ConfidentialReminderRepository(ApplicationDbContext context) : IConfidentialReminderRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Guid> AddConfidentialReminder(ConfidentialReminderEntity reminder, CancellationToken cancellationToken)
    {
        var existing = await _context.ConfidentialReminders
            .FirstOrDefaultAsync(r => r.CorrespondenceId == reminder.CorrespondenceId, cancellationToken);
        if (existing != null)
        {
            return existing.Id;
        }
        await _context.ConfidentialReminders.AddAsync(reminder, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return reminder.Id;
    }

    public async Task RemoveConfidentialReminderByCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var reminder = await _context.ConfidentialReminders.FirstOrDefaultAsync(r => r.CorrespondenceId == correspondenceId, cancellationToken);
        if (reminder == null)
        {
            return;
        }
        _context.ConfidentialReminders.Remove(reminder);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> NumberOfRemindersForRecipient(string recipient, CancellationToken cancellationToken)
    {
        var numberOfRemindersForRecipient = await _context.ConfidentialReminders.CountAsync(r => r.Recipient == recipient, cancellationToken);
        return numberOfRemindersForRecipient;
    }

    public async Task<bool> CorrespondenceHasReminder(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var reminderExists = await _context.ConfidentialReminders.AnyAsync(r => r.CorrespondenceId == correspondenceId, cancellationToken);
        return reminderExists;
    }

    public async Task<Guid?> GetDialogIdOfReminderForRecipient(string recipient, CancellationToken cancellationToken)
    {
        var reminder = await _context.ConfidentialReminders.FirstOrDefaultAsync(r => r.Recipient == recipient, cancellationToken);
        if (reminder == null)
        {
            return null;
        }
        return reminder.DialogId;
    }
}