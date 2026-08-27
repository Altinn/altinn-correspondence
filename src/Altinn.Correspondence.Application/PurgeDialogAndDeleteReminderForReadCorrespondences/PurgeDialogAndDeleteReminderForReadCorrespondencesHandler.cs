using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Hangfire;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Application.PurgeDialogAndDeleteReminderForReadCorrespondences;

public class PurgeDialogAndDeleteReminderForReadCorrespondencesHandler(
    IConfidentialReminderRepository confidentialReminderRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient,
    IConfidentialReminderDialogSynchronizer dialogSynchronizer,
    ILogger<PurgeDialogAndDeleteReminderForReadCorrespondencesHandler> logger) : IHandler<PurgeDialogAndDeleteReminderForReadCorrespondencesResponse>
{
    public Task<OneOf<PurgeDialogAndDeleteReminderForReadCorrespondencesResponse, Error>> Process(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Enqueueing deletion job for confidential reminders linked to read correspondences");

        var jobId = backgroundJobClient.Enqueue(() => ExecuteDeleteInBackground(CancellationToken.None));

        logger.LogInformation("Deletion job {jobId} has been enqueued", jobId);

        return Task.FromResult<OneOf<PurgeDialogAndDeleteReminderForReadCorrespondencesResponse, Error>>(new PurgeDialogAndDeleteReminderForReadCorrespondencesResponse
        {
            JobId = jobId,
            Message = "Deletion job has been enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 43200)]
    public async Task ExecuteDeleteInBackground(CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing deletion of confidential reminders linked to read correspondences");

        var totalProcessed = 0;
        var totalDeleted = 0;
        var totalSkipped = 0;
        var totalErrors = 0;
        var allErrors = new List<string>();

        try
        {
            var reminders = await confidentialReminderRepository.GetConfidentialRemindersLinkedToReadCorrespondences(
                cancellationToken);

            logger.LogInformation("Found {count} reminders to process", reminders.Count);

            foreach (var reminder in reminders)
            {
                try
                {
                    totalProcessed++;
                    var deleted = await ProcessSingleReminder(reminder, cancellationToken);
                    if (deleted)
                        totalDeleted++;
                    else
                        totalSkipped++;
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    allErrors.Add($"Error processing reminder {reminder.Id}: {ex.Message}");
                    logger.LogError(ex, "Failed to process reminder {reminderId}", reminder.Id);
                }
            }

            logger.LogInformation(
                "Deletion job completed. Total processed: {totalProcessed}, Deleted: {totalDeleted}, Skipped: {totalSkipped}, Errors: {totalErrors}",
                totalProcessed, totalDeleted, totalSkipped, totalErrors);

            if (allErrors.Count > 0)
            {
                logger.LogWarning("Deletion job completed with {errorCount} errors: {errors}", totalErrors, string.Join("; ", allErrors));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error during deletion of confidential reminders");
            throw;
        }
    }

    private async Task<bool> ProcessSingleReminder(ConfidentialReminderEntity reminder, CancellationToken cancellationToken)
    {
        Guid? dialogToSoftDelete = null;
        var deleted = false;

        try
        {
            await dialogSynchronizer.ExecuteForRecipientAsync(reminder.Recipient, async (ct) =>
            {
                // Re-read under lock: overview cleanup (or another purge pass) may have already removed this row.
                var targetReminder = await confidentialReminderRepository.GetByCorrespondenceId(
                    reminder.CorrespondenceId,
                    ct);
                if (targetReminder is null)
                {
                    logger.LogInformation(
                        "Confidential reminder for correspondence {correspondenceId} was already removed; skipping",
                        reminder.CorrespondenceId);
                    return;
                }

                var isFinalReminderForRecipient =
                    await confidentialReminderRepository.NumberOfRemindersForRecipient(targetReminder.Recipient, ct) == 1;

                if (isFinalReminderForRecipient)
                {
                    dialogToSoftDelete = targetReminder.DialogId;
                    if (!targetReminder.DialogId.HasValue)
                    {
                        logger.LogWarning("No DialogId found for confidential reminder {reminderId}, skipping dialog deletion", targetReminder.Id);
                    }
                }

                await confidentialReminderRepository.RemoveConfidentialReminderByCorrespondenceId(targetReminder.CorrespondenceId, ct);
                deleted = true;

                if (isFinalReminderForRecipient)
                {
                    // Closing state: drop the idempotency key under the same lock so creation cannot reuse this dialog.
                    await idempotencyKeyRepository.DeleteByPartyUrnAndTypeAsync(
                        targetReminder.Recipient,
                        IdempotencyType.ConfidentialReminderDialog,
                        ct);
                }

                logger.LogInformation(
                    "Deleted confidential reminder {reminderId} | CorrespondenceId: {correspondenceId} | DialogId: {dialogId}",
                    targetReminder.Id, targetReminder.CorrespondenceId, targetReminder.DialogId);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete confidential reminder {reminderId} for correspondence {correspondenceId}", reminder.Id, reminder.CorrespondenceId);
            throw;
        }

        if (dialogToSoftDelete.HasValue)
        {
            try
            {
                await dialogportenService.TrySoftDeleteDialog(dialogToSoftDelete.Value.ToString());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to soft delete dialog {dialogId} linked to confidential reminder {reminderId}", dialogToSoftDelete, reminder.Id);
                throw;
            }
        }

        return deleted;
    }
}
