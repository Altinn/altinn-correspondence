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
        await _context.ConfidentialReminders.AddAsync(reminder, cancellationToken);
        await _context.SaveChangesAsync();
        return reminder.Id;
    }

    public async Task RemoveConfidentialReminder(Guid correspondenceId, CancellationToken cancellationToken)
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

    public async Task<bool> RecipientHasConfidentialReminder(string recipient, CancellationToken cancellationToken)
    {
        var reminderExists = await _context.ConfidentialReminders.AnyAsync(r => r.Recipient == recipient, cancellationToken);
        logger.LogDebug("Checked for confidential reminder for recipient {Recipient}, exists: {Exists}", recipient, reminderExists);
        return reminderExists;
    }

    public async Task<bool> CorrespondenceHasReminder(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var reminderExists = await _context.ConfidentialReminders.AnyAsync(r => r.CorrespondenceId == correspondenceId, cancellationToken);
        logger.LogDebug("Checked for confidential reminder for correspondence {CorrespondenceId}, exists: {Exists}", correspondenceId, reminderExists);
        return reminderExists;
    }

    public async Task<string> GetDialogIdOfReminderForRecipient(string recipient, CancellationToken cancellationToken)
    {
        var reminder = await _context.ConfidentialReminders.FirstOrDefaultAsync(r => r.Recipient == recipient, cancellationToken);
        if (reminder == null)
        {
            logger.LogWarning("Attempted to get dialog id of confidential reminder for recipient {Recipient}, but no reminder was found", recipient);
            return string.Empty;
        }
        return reminder.DialogId.ToString();
    }
}