using System.Security.Claims;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.UpdateOldCorrespondencesWithDownloadAll;

public class UpdateOldCorrespondencesWithDownloadAllHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAttachmentRepository attachmentRepository,
    IBackgroundJobClient backgroundJobClient,
    ILogger<UpdateOldCorrespondencesWithDownloadAllHandler> logger) : IHandler<UpdateOldCorrespondencesWithDownloadAllRequest, UpdateOldCorrespondencesWithDownloadAllResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IBackgroundJobClient _backgroundJobClient = backgroundJobClient;
    private readonly ILogger<UpdateOldCorrespondencesWithDownloadAllHandler> _logger = logger;

    public Task<OneOf<UpdateOldCorrespondencesWithDownloadAllResponse, Error>> Process(UpdateOldCorrespondencesWithDownloadAllRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting update of old correspondences with download all. Window size: {windowSize}", request.windowSize);
        var jobId = _backgroundJobClient.Enqueue<UpdateOldCorrespondencesWithDownloadAllHandler>(
            HangfireQueues.LiveMigration,
            handler => handler.ExecutePatchingInBackground(request, CancellationToken.None));

        _logger.LogInformation("Orchestrator job {jobId} has been enqueued", jobId);

        return Task.FromResult<OneOf<UpdateOldCorrespondencesWithDownloadAllResponse, Error>>(new UpdateOldCorrespondencesWithDownloadAllResponse
        {
            JobId = jobId,
            Message = "Cleanup job has been Enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecutePatchingInBackground(UpdateOldCorrespondencesWithDownloadAllRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing batch starting after cursor {cursorId}", request.CursorId?.ToString().SanitizeForLogging());

        var queueLimit = request.windowSize * 2;
        var enqueuedJobs = JobStorage.Current.GetMonitoringApi().EnqueuedCount(HangfireQueues.Migration);
        if (enqueuedJobs >= queueLimit)
        {
            _logger.LogInformation(
                "Queue has {enqueuedJobs} jobs (limit {limit}), rescheduling in 1 minute",
                enqueuedJobs, queueLimit);
            _backgroundJobClient.Schedule<UpdateOldCorrespondencesWithDownloadAllHandler>(
                HangfireQueues.LiveMigration,
                handler => handler.ExecutePatchingInBackground(request, CancellationToken.None),
                TimeSpan.FromMinutes(1));
            return;
        }

        var window = await _correspondenceRepository.GetCorrespondencesWindowAfter(
            request.windowSize + 1, request.CursorCreated, request.CursorId, false, cancellationToken);

        var isMore = window.Count > request.windowSize;
        if (isMore) window.RemoveAt(window.Count - 1);

        if (window.Count == 0)
        {
            _logger.LogInformation(
                "No more correspondences to process. Job complete. Total processed: {processed}, Patched: {patched}, Not matching criteria: {notMatchingCriteria}, Errors: {errors}",
                request.TotalProcessed, request.TotalPatched, request.TotalNotMatchingCriteria, request.TotalErrors);
            return;
        }

        var batchProcessed = 0;
        var batchPatched = 0;
        var batchNotMatchingCriteria = 0;
        var batchErrors = 0;

        foreach (var correspondence in window)
        {
            try
            {
                batchProcessed++;
                var attachments = await _attachmentRepository.GetAttachmentsByCorrespondence(correspondence.Id, cancellationToken);
                if (attachments != null && attachments.Count >= 2 && attachments.Sum(a => a.AttachmentSize) <= 2_000_000_000) // 2 GB
                {
                    ProcessSingleCorrespondence(correspondence);
                    batchPatched++;
                }
                else
                {
                    batchNotMatchingCriteria++;
                }
            }
            catch (Exception ex)
            {
                batchErrors++;
                _logger.LogError(ex, "Error processing correspondence {correspondenceId}", correspondence.Id);
            }
        }

        var totalProcessed = request.TotalProcessed + batchProcessed;
        var totalPatched = request.TotalPatched + batchPatched;
        var totalNotMatchingCriteria = request.TotalNotMatchingCriteria + batchNotMatchingCriteria;
        var totalErrors = request.TotalErrors + batchErrors;

        if (isMore)
        {
            var last = window[^1];
            var nextRequest = new UpdateOldCorrespondencesWithDownloadAllRequest
            {
                windowSize = request.windowSize,
                CursorCreated = last.Created,
                CursorId = last.Id,
                TotalProcessed = totalProcessed,
                TotalPatched = totalPatched,
                TotalNotMatchingCriteria = totalNotMatchingCriteria,
                TotalErrors = totalErrors
            };
            _backgroundJobClient.Enqueue<UpdateOldCorrespondencesWithDownloadAllHandler>(
                HangfireQueues.LiveMigration,
                handler => handler.ExecutePatchingInBackground(nextRequest, CancellationToken.None));
            _logger.LogInformation(
                "Batch complete. Processed: {processed}, Patched: {patched}, Not matching criteria: {notMatchingCriteria}, Errors: {errors}",
                batchProcessed, batchPatched, batchNotMatchingCriteria, batchErrors);
        }
        else
        {
            _logger.LogInformation(
                "No more correspondences to process. Job complete. Total processed: {processed}, Patched: {patched}, Not matching criteria: {notMatchingCriteria}, Errors: {errors}",
                totalProcessed, totalPatched, totalNotMatchingCriteria, totalErrors);
        }
    }

    private void ProcessSingleCorrespondence(CorrespondenceEntity correspondence)
    {
        _backgroundJobClient.Enqueue<IDialogportenService>(
            HangfireQueues.Migration,
            service => service.TryAddDownloadAllAttachmentsToDialog(correspondence.Id, CancellationToken.None));
    }
}
