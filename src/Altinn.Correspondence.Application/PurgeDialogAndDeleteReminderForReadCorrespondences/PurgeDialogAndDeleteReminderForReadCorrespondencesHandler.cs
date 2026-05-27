using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Hangfire;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Application.PurgeDialogAndDeleteReminderForReadCorrespondences;

public class PurgeDialogAndDeleteReminderForReadCorrespondencesHandler(
    IConfidentialReminderRepository confidentialReminderRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient,
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
        try
        {
            if (await confidentialReminderRepository.NumberOfRemindersForRecipient(reminder.Recipient, cancellationToken) == 1){

            if (!reminder.DialogId.HasValue)
            {
                logger.LogWarning("No DialogId found for confidential reminder {reminderId}, skipping dialog deletion", reminder.Id);
                return true;
            }
            await dialogportenService.TrySoftDeleteDialog(reminder.DialogId.Value.ToString());
        }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to soft delete dialog {dialogId} linked to confidential reminder {reminderId}", reminder.DialogId, reminder.Id);
            throw;
        }
        try
        {
        await confidentialReminderRepository.RemoveConfidentialReminderByCorrespondenceId(reminder.CorrespondenceId, cancellationToken);
        logger.LogInformation(
            "Deleted confidential reminder {reminderId} | CorrespondenceId: {correspondenceId} | DialogId: {dialogId}",
            reminder.Id, reminder.CorrespondenceId, reminder.DialogId);
            
        } catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete confidential reminder {reminderId} for correspondence {correspondenceId}", reminder.Id, reminder.CorrespondenceId);
            throw;
        }

        return true;
    }
}