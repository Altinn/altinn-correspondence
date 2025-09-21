
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Hangfire;

namespace Altinn.Correspondence.Application.CleanupOrphanedDialogs;

public class CleanupOrphanedDialogsHandler(
    ICorrespondenceRepository correspondenceRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CleanupOrphanedDialogsHandler> logger) : IHandler<CleanupOrphanedDialogsRequest, CleanupOrphanedDialogsResponse>
{
    public Task<OneOf<CleanupOrphanedDialogsResponse, Error>> Process(CleanupOrphanedDialogsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting cleanup of orphaned dialogs with window size {windowSize}", request.WindowSize);

        var jobId = backgroundJobClient.Enqueue(() => ExecuteCleanupInBackground(request.WindowSize, CancellationToken.None));

        logger.LogInformation("Cleanup job {jobId} has been enqueued", jobId);

        return Task.FromResult<OneOf<CleanupOrphanedDialogsResponse, Error>>(new CleanupOrphanedDialogsResponse
        {
            JobId = jobId,
            Message = "Cleanup job has been Enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 14400)]
    public async Task ExecuteCleanupInBackground(int windowSize, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing cleanup of orphaned dialogs in background job");
        
        var totalProcessed = 0;
        var totalDeleted = 0;
        var totalErrors = 0;
        var totalAlreadyDeleted = 0;
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
                var purgedCorrespondences = await correspondenceRepository.GetCorrespondencesByIdsWithExternalReferenceAndCurrentStatus(
                    windowIds,
                    ReferenceType.DialogportenDialogId,
                    new List<CorrespondenceStatus> { CorrespondenceStatus.PurgedByAltinn, CorrespondenceStatus.PurgedByRecipient },
                    cancellationToken);

                logger.LogInformation(
                    "Scanned {scanned} correspondences, {candidates} purged correspondences to delete dialogs for (IsMore: {isMore})",
                    correspondencesWindow.Count,
                    purgedCorrespondences.Count,
                    isMoreCorrespondences);

                foreach (var correspondence in purgedCorrespondences)
                {
                    try
                    {
                        var (deleted, alreadyDeleted) = await ProcessSingleCorrespondence(correspondence);
                        if (deleted) totalDeleted++;
                        if (alreadyDeleted) totalAlreadyDeleted++;
                    }
                    catch (Exception ex)
                    {
                        totalErrors++;
                        var errorMessage = $"Failed to process correspondence {correspondence.Id}: {ex.Message}";
                        allErrors.Add(errorMessage);
                        logger.LogError(ex, "Failed to process correspondence {correspondenceId}", correspondence.Id);
                    }
                }

                totalProcessed += purgedCorrespondences.Count;

                if (correspondencesWindow.Count == 0)
                {
                    isMoreCorrespondences = false;
                }
            }

            logger.LogInformation("Background cleanup completed. Total processed: {processedCount}, Total deleted: {deletedCount}, Already deleted: {alreadyDeletedCount}, Total errors: {errorCount}", 
                totalProcessed, totalDeleted, totalAlreadyDeleted, totalErrors);
                
            if (allErrors.Count > 0)
            {
                logger.LogWarning("Cleanup completed with {errorCount} errors: {errors}", totalErrors, string.Join("; ", allErrors));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute background cleanup of orphaned dialogs");
            throw;
        }
    }

    private async Task<(bool deleted, bool alreadyDeleted)> ProcessSingleCorrespondence(CorrespondenceEntity correspondence)
    {
        var dialogId = correspondence.ExternalReferences
            .FirstOrDefault(er => er.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;

        if (dialogId == null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping purging correspondence {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog",
                    correspondence.Id);
                return (false, false);
            }
            logger.LogError("Correspondence {correspondenceId} has no dialog reference", correspondence.Id);
            return (false, false);
        }

        logger.LogInformation("Attempting to delete dialog {dialogId} for purged correspondence {correspondenceId}", 
            dialogId, correspondence.Id);

        var deleted = await dialogportenService.TrySoftDeleteDialog(dialogId);
        if (deleted)
        {
            logger.LogInformation("Successfully deleted dialog {dialogId} for correspondence {correspondenceId}", dialogId, correspondence.Id);
            return (true, false);
        }
        logger.LogInformation("Dialog {dialogId} already deleted in Dialogporten for correspondence {correspondenceId}", dialogId, correspondence.Id);
        return (false, true);
    }
}