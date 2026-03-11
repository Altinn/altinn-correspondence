using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories;

public class ConfidentialReminderRepository(ApplicationDbContext context, ILogger<IConfidentialReminderRepository> logger) : IConfidentialReminderRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Guid> AddConfidentialReminder(ConfidentialReminderEntity reminder, CancellationToken cancellationToken)
    {
        logger.LogDebug("Adding confidential reminder for correspondence {CorrespondenceId}", reminder.CorrespondenceId);
        var existing = await _context.ConfidentialReminders
            .FirstOrDefaultAsync(r => r.CorrespondenceId == reminder.CorrespondenceId, cancellationToken);
        if (existing != null)
        {
            logger.LogWarning("Confidential reminder for correspondence {CorrespondenceId} already exists, skipping duplicate insert", reminder.CorrespondenceId);
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
            logger.LogWarning("Attempted to remove confidential reminder for correspondence {CorrespondenceId}, but it was not found", correspondenceId);
            return;
        }
        _context.ConfidentialReminders.Remove(reminder);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> NumberOfRemindersForRecipient(string recipient, CancellationToken cancellationToken)
    {
        var numberOfRemindersForRecipient = await _context.ConfidentialReminders.CountAsync(r => r.Recipient == recipient, cancellationToken);
        logger.LogDebug("Checked for confidential reminder for recipient {Recipient}, count: {Count}", recipient, numberOfRemindersForRecipient);
        return numberOfRemindersForRecipient;
    }

    public async Task<bool> CorrespondenceHasReminder(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var reminderExists = await _context.ConfidentialReminders.AnyAsync(r => r.CorrespondenceId == correspondenceId, cancellationToken);
        logger.LogDebug("Checked for confidential reminder for correspondence {CorrespondenceId}, exists: {Exists}", correspondenceId, reminderExists);
        return reminderExists;
    }

    public async Task<Guid?> GetDialogIdOfReminderForRecipient(string recipient, CancellationToken cancellationToken)
    {
        var reminder = await _context.ConfidentialReminders.FirstOrDefaultAsync(r => r.Recipient == recipient, cancellationToken);
        if (reminder == null)
        {
            logger.LogWarning("Attempted to get dialog id of confidential reminder for recipient {Recipient}, but no reminder was found", recipient);
            return null;
        }
        return reminder.DialogId;
    }
}