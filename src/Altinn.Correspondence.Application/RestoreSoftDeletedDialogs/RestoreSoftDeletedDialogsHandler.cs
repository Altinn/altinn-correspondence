using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.RestoreSoftDeletedDialogs;

public class RestoreSoftDeletedDialogsHandler(
    ICorrespondenceRepository correspondenceRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<RestoreSoftDeletedDialogsHandler> logger) : IHandler<RestoreSoftDeletedDialogsRequest, RestoreSoftDeletedDialogsResponse>
{
    public async Task<OneOf<RestoreSoftDeletedDialogsResponse, Error>> Process(RestoreSoftDeletedDialogsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (request.DryRun)
        {
            logger.LogInformation("Starting dry run check of soft-deleted dialogs with window size {windowSize}", request.WindowSize);
            
            var (totalProcessed, totalAlreadyDeleted, totalNotDeleted, totalErrors) = await ExecuteDryRun(request.WindowSize, cancellationToken);
            
            logger.LogInformation("Dry run completed. Total processed: {processedCount}, Already deleted: {alreadyDeletedCount}, Not deleted: {notDeletedCount}, Total errors: {errorCount}", 
                totalProcessed, totalAlreadyDeleted, totalNotDeleted, totalErrors);

            return new RestoreSoftDeletedDialogsResponse
            {
                Message = "Dry run completed",
                TotalProcessed = totalProcessed,
                TotalAlreadyDeleted = totalAlreadyDeleted,
                TotalNotDeleted = totalNotDeleted,
                TotalErrors = totalErrors
            };
        }
        else
        {
            logger.LogInformation("Starting restore of soft-deleted dialogs with window size {windowSize}", request.WindowSize);

            var jobId = backgroundJobClient.Enqueue(() => ExecuteRestoreInBackground(request.WindowSize, request.DryRun, CancellationToken.None));

            logger.LogInformation("Restore job {jobId} has been enqueued", jobId);

            return new RestoreSoftDeletedDialogsResponse
            {
                JobId = jobId,
                Message = "Restore job has been Enqueued"
            };
        }
    }

    private async Task<(int totalProcessed, int totalAlreadyDeleted, int totalNotDeleted, int totalErrors)> ExecuteDryRun(int windowSize, CancellationToken cancellationToken)
    {
        var totalProcessed = 0;
        var totalAlreadyDeleted = 0;
        var totalNotDeleted = 0;
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
                    "Scanned {scanned} correspondences, {candidates} to check deletion status for (IsMore: {isMore})",
                    correspondencesWindow.Count,
                    nonPurgedWithDialog.Count,
                    isMoreCorrespondences);

                foreach (var correspondence in nonPurgedWithDialog)
                {
                    try
                    {
                        var (_, _, alreadyDeleted, notDeleted) = await ProcessSingleCorrespondence(correspondence, true);
                        if (alreadyDeleted) totalAlreadyDeleted++;
                        if (notDeleted) totalNotDeleted++;
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

            if (allErrors.Count > 0)
            {
                logger.LogWarning("Dry run completed with {errorCount} errors: {errors}", totalErrors, string.Join("; ", allErrors));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute dry run of soft-deleted dialogs");
            throw;
        }

        return (totalProcessed, totalAlreadyDeleted, totalNotDeleted, totalErrors);
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ExecuteRestoreInBackground(int windowSize, bool dryRun, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing restore of soft-deleted dialogs in background job (DryRun: {dryRun})", dryRun);
        
        var totalProcessed = 0;
        var totalRestored = 0;
        var totalAlreadyActive = 0;
        var totalAlreadyDeleted = 0;
        var totalNotDeleted = 0;
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
                        var (restored, alreadyActive, alreadyDeleted, notDeleted) = await ProcessSingleCorrespondence(correspondence, dryRun);
                        if (restored) totalRestored++;
                        if (alreadyActive) totalAlreadyActive++;
                        if (alreadyDeleted) totalAlreadyDeleted++;
                        if (notDeleted) totalNotDeleted++;
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

            if (dryRun)
            {
                logger.LogInformation("Background dry run completed. Total processed: {processedCount}, Already deleted: {alreadyDeletedCount}, Not deleted: {notDeletedCount}, Total errors: {errorCount}", 
                    totalProcessed, totalAlreadyDeleted, totalNotDeleted, totalErrors);
            }
            else
            {
                logger.LogInformation("Background restore completed. Total processed: {processedCount}, Total restored: {restoredCount}, Already active: {alreadyActiveCount}, Total errors: {errorCount}", 
                    totalProcessed, totalRestored, totalAlreadyActive, totalErrors);
            }
                
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

    private async Task<(bool restored, bool alreadyActive, bool alreadyDeleted, bool notDeleted)> ProcessSingleCorrespondence(CorrespondenceEntity correspondence, bool dryRun)
    {
        var dialogId = correspondence.ExternalReferences
            .FirstOrDefault(er => er.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;

        if (dialogId == null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping correspondence {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog",
                    correspondence.Id);
                return (false, false, false, false);
            }
            logger.LogError("Correspondence {correspondenceId} has no dialog reference", correspondence.Id);
            return (false, false, false, false);
        }

        if (dryRun)
        {
            logger.LogInformation("Checking deletion status of dialog {dialogId} for correspondence {correspondenceId}", 
                dialogId, correspondence.Id);

            var dialogDeleted = await dialogportenService.HasDialogBeenDeleted(dialogId);
            if (dialogDeleted)
            {
                logger.LogWarning("Dialog {dialogId} is already deleted for correspondence {correspondenceId}", 
                    dialogId, correspondence.Id);
                return (false, false, true, false);
            }
            else
            {
                return (false, false, false, true);
            }
        }
        else
        {
            logger.LogInformation("Attempting to restore soft-deleted dialog {dialogId} for correspondence {correspondenceId}", 
                dialogId, correspondence.Id);

            var restored = await dialogportenService.TryRestoreSoftDeletedDialog(dialogId);
            if (restored)
            {
                logger.LogInformation("Successfully restored dialog {dialogId} for correspondence {correspondenceId}", dialogId, correspondence.Id);
                return (true, false, false, false);
            }
            logger.LogInformation("Dialog {dialogId} was not restored (possibly already active) for correspondence {correspondenceId}", dialogId, correspondence.Id);
            return (false, true, false, false);
        }
    }
} 