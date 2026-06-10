using System.Security.Claims;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.UpdateOldCorrespondencesWithDownloadAll;

public class UpdateOldCorrespondencesWithDownloadAllHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAttachmentRepository attachmentRepository,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    ILogger<UpdateOldCorrespondencesWithDownloadAllHandler> logger) : IHandler<UpdateOldCorrespondencesWithDownloadAllRequest, UpdateOldCorrespondencesWithDownloadAllResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IBackgroundJobClient _backgroundJobClient = backgroundJobClient;
    private readonly ILogger<UpdateOldCorrespondencesWithDownloadAllHandler> _logger = logger;

    public Task<OneOf<UpdateOldCorrespondencesWithDownloadAllResponse, Error>> Process(UpdateOldCorrespondencesWithDownloadAllRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting update of old correspondences with download all. Window size: {windowSize}", request.windowSize);
        var jobId = _backgroundJobClient.Enqueue(() => ExecutePatchingInBackground(request.windowSize, CancellationToken.None));

        _logger.LogInformation("Cleanup job {jobId} has been enqueued", jobId);

        return Task.FromResult<OneOf<UpdateOldCorrespondencesWithDownloadAllResponse, Error>>(new UpdateOldCorrespondencesWithDownloadAllResponse
        {
            JobId = jobId,
            Message = "Cleanup job has been Enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 43200)]
    public async Task ExecutePatchingInBackground(int windowSize, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing update of old correspondences with download all in background job");

        var totalProcessed = 0;
        var totalPatched = 0;
        var totalAlreadyHadDownloadAll = 0;
        var totalNotMatchingDownloadAllCriteria = 0;
        var totalErrors = 0;
        var allErrors = new List<string>();

        try
        {
            DateTimeOffset? lastCreated = null;
            Guid? lastId = null;
            bool isMoreCorrespondences = true;

            while (isMoreCorrespondences)
            {
                _logger.LogInformation("Processing batch starting after cursor {correspondenceId}", lastId);
                var correspondencesWindow = await _correspondenceRepository.GetCorrespondencesWindowAfter
                (windowSize + 1,
                lastCreated,
                lastId,
                true,
                cancellationToken);

                isMoreCorrespondences = correspondencesWindow.Count > windowSize;
                if (isMoreCorrespondences)
                {
                    correspondencesWindow.RemoveAt(correspondencesWindow.Count - 1);
                }

                if (correspondencesWindow.Count > 0)
                {
                    var last = correspondencesWindow[^1];
                    lastCreated = last.Created;
                    lastId = last.Id;
                }

                foreach (var correspondence in correspondencesWindow)
                {
                    try
                    {
                        totalProcessed++;
                        var attachments = await _attachmentRepository.GetAttachmentsByCorrespondence(correspondence.Id, cancellationToken);
                        if (attachments != null && attachments.Count >= 2)
                        {
                            if (attachments.Sum(a => a.AttachmentSize) <= 2_000_000_000) // 2 GB
                            {
                                var dialogId = correspondence.ExternalReferences.FirstOrDefault(er => er.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
                                if (dialogId == null)
                                {
                                    totalErrors++;
                                    continue;
                                }
                                bool hasDownloadAll = await dialogportenService.HasDownloadAllAttachments(dialogId, cancellationToken);
                                if (hasDownloadAll){
                                    totalAlreadyHadDownloadAll++;
                                    continue;
                                } 
                                var patched = await ProcessSingleCorrespondence(correspondence, cancellationToken);
                                if (patched){
                                    totalPatched++;
                                }
                            }
                            else
                            {
                                totalNotMatchingDownloadAllCriteria++;
                            }
                        } else
                        {
                            totalNotMatchingDownloadAllCriteria++;
                        }
                    }
                    catch (Exception ex)
                    {
                        totalErrors++;
                        var errorMessage = $"Error processing correspondence {correspondence.Id}: {ex.Message}";
                        allErrors.Add(errorMessage);
                        _logger.LogError(ex, errorMessage);
                    }
                }
                if (correspondencesWindow.Count == 0)
                {
                    isMoreCorrespondences = false;
                }
            }
            logger.LogInformation("Background update completed. Total processed: {processedCount}, Total patched: {patchedCount}, Already ok: {alreadyOkCount}, Total errors: {errorCount}, Not matching criteria: {notMatchingCriteriaCount}", 
                totalProcessed, totalPatched, totalAlreadyHadDownloadAll, totalErrors, totalNotMatchingDownloadAllCriteria);
                
            if (allErrors.Count > 0)
            {
                logger.LogWarning("Background update completed with {errorCount} errors: {errors}", totalErrors, string.Join("; ", allErrors));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute background update of old correspondences with download all");
            throw;
        }
    }

    private async Task<bool> ProcessSingleCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(er => er.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId == null)
        {
            _logger.LogWarning("Correspondence {correspondenceId} has no DialogportenDialogId reference, skipping", correspondence.Id);
            return false;
        }
        try
        {
            _backgroundJobClient.Enqueue<IDialogportenService>(service => service.TryAddDownloadAllAttachmentsToDialog(dialogId, correspondence, CancellationToken.None));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to patch correspondence {correspondenceId} with download all information activity", correspondence.Id);
            throw;
        }
    }
}