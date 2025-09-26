using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Hangfire;

namespace Altinn.Correspondence.Application.CleanupPerishingDialogs;

public class CleanupPerishingDialogsHandler(
    ICorrespondenceRepository correspondenceRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CleanupPerishingDialogsHandler> logger) : IHandler<CleanupPerishingDialogsRequest, CleanupPerishingDialogsResponse>
{
    public Task<OneOf<CleanupPerishingDialogsResponse, Error>> Process(CleanupPerishingDialogsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting cleanup of perishing dialogs (removing expiresAt) with window size {windowSize}", request.WindowSize);

        var jobId = backgroundJobClient.Enqueue(() => ExecuteCleanupInBackground(request.WindowSize, CancellationToken.None));

        logger.LogInformation("Cleanup job {jobId} has been enqueued", jobId);

        return Task.FromResult<OneOf<CleanupPerishingDialogsResponse, Error>>(new CleanupPerishingDialogsResponse
        {
            JobId = jobId,
            Message = "Cleanup job has been Enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 43200)]
    public async Task ExecuteCleanupInBackground(int windowSize, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing cleanup of perishing dialogs in background job");
        
        var totalProcessed = 0;
        var totalPatched = 0;
        var totalAlreadyOk = 0;
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
                var candidates = await correspondenceRepository.GetCorrespondencesByIdsWithExternalReferenceAndAllowSystemDeleteAfter(
                    windowIds,
                    ReferenceType.DialogportenDialogId,
                    cancellationToken);

                logger.LogInformation(
                    "Scanned {scanned} correspondences, {candidates} to remove expiresAt for (IsMore: {isMore})",
                    correspondencesWindow.Count,
                    candidates.Count,
                    isMoreCorrespondences);

                foreach (var correspondence in candidates)
                {
                    try
                    {
                        var (patched, alreadyOk) = await ProcessSingleCorrespondence(correspondence);
                        if (patched) totalPatched++;
                        if (alreadyOk) totalAlreadyOk++;
                    }
                    catch (Exception ex)
                    {
                        totalErrors++;
                        var errorMessage = $"Failed to process correspondence {correspondence.Id}: {ex.Message}";
                        allErrors.Add(errorMessage);
                        logger.LogError(ex, "Failed to process correspondence {correspondenceId}", correspondence.Id);
                    }
                }

                totalProcessed += candidates.Count;

                if (correspondencesWindow.Count == 0)
                {
                    isMoreCorrespondences = false;
                }
            }

            logger.LogInformation("Background cleanup completed. Total processed: {processedCount}, Total patched: {patchedCount}, Already ok: {alreadyOkCount}, Total errors: {errorCount}", 
                totalProcessed, totalPatched, totalAlreadyOk, totalErrors);
                
            if (allErrors.Count > 0)
            {
                logger.LogWarning("Cleanup completed with {errorCount} errors: {errors}", totalErrors, string.Join("; ", allErrors));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute background cleanup of perishing dialogs");
            throw;
        }
    }

    private async Task<(bool patched, bool alreadyOk)> ProcessSingleCorrespondence(CorrespondenceEntity correspondence)
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

        logger.LogInformation("Attempting to remove expiresAt on dialog {dialogId} for correspondence {correspondenceId}", 
            dialogId, correspondence.Id);

        var removed = await dialogportenService.TryRemoveDialogExpiresAt(dialogId);
        if (removed)
        {
            logger.LogInformation("Successfully removed expiresAt on dialog {dialogId} for correspondence {correspondenceId}", dialogId, correspondence.Id);
            return (true, false);
        }
        logger.LogInformation("Dialog {dialogId} already has no expiresAt in Dialogporten for correspondence {correspondenceId}", dialogId, correspondence.Id);
        return (false, true);
    }
} 