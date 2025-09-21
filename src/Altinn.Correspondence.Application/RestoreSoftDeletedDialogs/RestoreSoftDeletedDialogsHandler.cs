using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Hangfire;

namespace Altinn.Correspondence.Application.RestoreSoftDeletedDialogs;

public class RestoreSoftDeletedDialogsHandler(
    ICorrespondenceRepository correspondenceRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<RestoreSoftDeletedDialogsHandler> logger) : IHandler<RestoreSoftDeletedDialogsRequest, RestoreSoftDeletedDialogsResponse>
{
    public Task<OneOf<RestoreSoftDeletedDialogsResponse, Error>> Process(RestoreSoftDeletedDialogsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting restore of soft-deleted dialogs with window size {windowSize}", request.WindowSize);

        var jobId = backgroundJobClient.Enqueue(() => ExecuteRestoreInBackground(request.WindowSize, CancellationToken.None));

        logger.LogInformation("Restore job {jobId} has been enqueued", jobId);

        return Task.FromResult<OneOf<RestoreSoftDeletedDialogsResponse, Error>>(new RestoreSoftDeletedDialogsResponse
        {
            JobId = jobId,
            Message = "Restore job has been Enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ExecuteRestoreInBackground(int windowSize, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing restore of soft-deleted dialogs in background job");
        
        var totalProcessed = 0;
        var totalRestored = 0;
        var totalAlreadyActive = 0;
        var totalErrors = 0;
        var allErrors = new List<string>();

        try
        {
            DateTimeOffset? lastCreated = null;
            Guid? lastId = null;
            bool isMoreCorrespondences = true;
            
            while (isMoreCorrespondences)
            {
                logger.LogInformation("Processing batch starting after cursor {lastCreated} / {lastId}", lastCreated, lastId);
                
                var correspondencesWindow = await correspondenceRepository.GetCorrespondencesWindowAfter(
                    windowSize + 1,
                    lastCreated,
                    lastId,
                    true,
                    cancellationToken);
                    
                isMoreCorrespondences = correspondencesWindow.Count > windowSize;
                if (isMoreCorrespondences)
                {
                    correspondencesWindow = correspondencesWindow.Take(windowSize).ToList();
                }

                if (correspondencesWindow.Count > 0)
                {
                    var last = correspondencesWindow[^1];
                    lastCreated = last.Created;
                    lastId = last.Id;
                }
                
                var windowIds = correspondencesWindow.Select(c => c.Id).ToList();
                var nonPurgedWithDialog = await correspondenceRepository.GetCorrespondencesByIdsWithExternalReferenceAndNotCurrentStatuses(
                    windowIds,
                    ReferenceType.DialogportenDialogId,
                    new List<CorrespondenceStatus> { CorrespondenceStatus.PurgedByAltinn, CorrespondenceStatus.PurgedByRecipient },
                    cancellationToken);

                logger.LogInformation(
                    "Scanned {scanned} correspondences, {candidates} to restore dialogs for (IsMore: {isMore})",
                    correspondencesWindow.Count,
                    nonPurgedWithDialog.Count,
                    isMoreCorrespondences);

                foreach (var correspondence in nonPurgedWithDialog)
                {
                    try
                    {
                        var (restored, alreadyActive) = await ProcessSingleCorrespondence(correspondence);
                        if (restored) totalRestored++;
                        if (alreadyActive) totalAlreadyActive++;
                    }
                    catch (Exception ex)
                    {
                        totalErrors++;
                        var errorMessage = $"Failed to process correspondence {correspondence.Id}: {ex.Message}";
                        allErrors.Add(errorMessage);
                        logger.LogError(ex, "Failed to process correspondence {correspondenceId}", correspondence.Id);
                    }
                }

                totalProcessed += nonPurgedWithDialog.Count;

                if (correspondencesWindow.Count == 0)
                {
                    isMoreCorrespondences = false;
                }
            }

            logger.LogInformation("Background restore completed. Total processed: {processedCount}, Total restored: {restoredCount}, Already active: {alreadyActiveCount}, Total errors: {errorCount}", 
                totalProcessed, totalRestored, totalAlreadyActive, totalErrors);
                
            if (allErrors.Count > 0)
            {
                logger.LogWarning("Restore completed with {errorCount} errors: {errors}", totalErrors, string.Join("; ", allErrors));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute background restore of soft-deleted dialogs");
            throw;
        }
    }

    private async Task<(bool restored, bool alreadyActive)> ProcessSingleCorrespondence(CorrespondenceEntity correspondence)
    {
        var dialogId = correspondence.ExternalReferences
            .FirstOrDefault(er => er.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;

        if (dialogId == null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping correspondence {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog",
                    correspondence.Id);
                return (false, false);
            }
            logger.LogError("Correspondence {correspondenceId} has no dialog reference", correspondence.Id);
            return (false, false);
        }

        logger.LogInformation("Attempting to restore soft-deleted dialog {dialogId} for correspondence {correspondenceId}", 
            dialogId, correspondence.Id);

        var restored = await dialogportenService.TryRestoreSoftDeletedDialog(dialogId);
        if (restored)
        {
            logger.LogInformation("Successfully restored dialog {dialogId} for correspondence {correspondenceId}", dialogId, correspondence.Id);
            return (true, false);
        }
        logger.LogInformation("Dialog {dialogId} was not restored (possibly already active) for correspondence {correspondenceId}", dialogId, correspondence.Id);
        return (false, true);
    }
} 