using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Hangfire;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Application.CleanupMarkdownAndHTMLInSummary;

public class CleanupMarkdownAndHTMLInSummaryHandler(
    ICorrespondenceRepository correspondenceRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CleanupMarkdownAndHTMLInSummaryHandler> logger) : IHandler<CleanupMarkdownAndHTMLInSummaryRequest, CleanupMarkdownAndHTMLInSummaryResponse>
{
    public Task<OneOf<CleanupMarkdownAndHTMLInSummaryResponse, Error>> Process(CleanupMarkdownAndHTMLInSummaryRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting cleanup of dialogs with markdown or html in summary with window size {windowSize}", request.WindowSize);

        var jobId = backgroundJobClient.Enqueue(() => ExecuteCleanupInBackground(request.WindowSize, CancellationToken.None));

        logger.LogInformation("Cleanup job {jobId} has been enqueued", jobId);

        return Task.FromResult<OneOf<CleanupMarkdownAndHTMLInSummaryResponse, Error>>(new CleanupMarkdownAndHTMLInSummaryResponse
        {
            JobId = jobId,
            Message = "Cleanup job has been Enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 43200)]
    public async Task ExecuteCleanupInBackground(int windowSize, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing cleanup of dialogs with markdown or html in summary in background job");

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
                logger.LogInformation("Processing batch starting after cursor {correspondenceId}", lastId);
                var correspondencesWindow = await correspondenceRepository.GetCorrespondencesWindowAfter
                (windowSize + 1,
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
                var candidates = await correspondenceRepository.GetCorrespondencesByNoAltinn2IdAndExistingDialog(
                    windowIds,
                    ReferenceType.DialogportenDialogId,
                    cancellationToken);
                logger.LogInformation("Found {candidateCount} candidates for cleanup in current window", candidates.Count);

                foreach (var correspondence in candidates)
                {
                    try
                    {
                        totalProcessed++;
                        var (patched, alreadyOk) = await ProcessSingleCorrespondence(correspondence);
                        if (patched)
                        {
                            totalPatched++;
                        }
                        else if (alreadyOk)
                        {
                            totalAlreadyOk++;
                        }
                    }
                    catch (Exception ex)
                    {
                        totalErrors++;
                        var errorMessage = $"Error processing correspondence {correspondence.Id}: {ex.Message}";
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
            logger.LogError(ex, "Failed to execute background cleanup of dialogs with markdown or html in summary");
            throw;
        }
    }

    private async Task<(bool patched, bool alreadyOk)> ProcessSingleCorrespondence(CorrespondenceEntity correspondence)
    {
        var dialogId = correspondence.ExternalReferences
            .FirstOrDefault(er => er.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
            var originalSummary = correspondence.Content?.MessageSummary;

            if (originalSummary == null)
            {
                logger.LogWarning("Skipping correspondence {correspondenceId} as it has no summary", correspondence.Id);
                return (false, false);
            }
            else
            {
                var newSummary = TextValidation.StripSummaryForHtmlAndMarkdown(originalSummary);
                if (newSummary.ToString().Length < originalSummary.ToString().Length)
                {
                    logger.LogInformation("Attempting to update summary on correspondence {correspondenceId}", correspondence.Id);
                    var updated = await dialogportenService.TryRemoveMarkdownAndHtmlFromSummary(dialogId, newSummary);
                    if (updated)
                    {
                        logger.LogInformation("Successfully updated summary on correspondence {correspondenceId}", correspondence.Id);
                        return (true, false);
                    }
                    else
                    {
                        logger.LogWarning("Failed to update summary on correspondence {correspondenceId}", correspondence.Id);
                        return (false, false);
                    }
                }
                logger.LogInformation("No markdown or html found in summary for correspondence {correspondenceId}, no update needed", correspondence.Id);
                return (false, true);
            }
    }
} 