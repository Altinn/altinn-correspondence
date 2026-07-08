using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.UpdateOldCorrespondencesWithDownloadAll;

public class UpdateOldCorrespondencesWithDownloadAllBatchJob(
    ICorrespondenceRepository correspondenceRepository,
    IAttachmentRepository attachmentRepository,
    IBackgroundJobClient backgroundJobClient,
    ILogger<UpdateOldCorrespondencesWithDownloadAllBatchJob> logger)
{
    private const long MaxTotalAttachmentSizeBytes = 2_000_000_000;

    public ChainedBatchJobDefinition<UpdateOldCorrespondencesWithDownloadAllRequest, CorrespondenceEntity> CreateDefinition() =>
        new()
        {
            Settings = new ChainedBatchJobSettings
            {
                JobName = "UpdateOldCorrespondencesWithDownloadAll",
                BatchSize = 1,
                BackpressureLimit = 0,
            },
            ResolveBackpressureLimit = request => request.windowSize * 2,
            FetchBatchAsync = FetchBatchAsync,
            GetCursorFromItem = correspondence => new KeysetCursor(correspondence.Created, correspondence.Id),
            ProcessBatchAsync = ProcessBatchAsync,
            CreateNextState = (request, cursor, _) => new UpdateOldCorrespondencesWithDownloadAllRequest
            {
                windowSize = request.windowSize,
                CursorCreated = cursor.Created,
                CursorId = cursor.Id,
                TotalProcessed = request.TotalProcessed,
                TotalPatched = request.TotalPatched,
                TotalNotMatchingCriteria = request.TotalNotMatchingCriteria,
                TotalErrors = request.TotalErrors,
            },
            EnqueueNextBatch = nextState =>
                backgroundJobClient.Enqueue<UpdateOldCorrespondencesWithDownloadAllHandler>(
                    ChainedBatchJobQueues.Orchestrator,
                    handler => handler.ExecutePatchingInBackground(nextState, CancellationToken.None)),
            RescheduleBatch = state =>
                backgroundJobClient.Schedule<UpdateOldCorrespondencesWithDownloadAllHandler>(
                    ChainedBatchJobQueues.Orchestrator,
                    handler => handler.ExecutePatchingInBackground(state, CancellationToken.None),
                    TimeSpan.FromMinutes(1)),
            OnComplete = request => logger.LogInformation(
                "No more correspondences to process. Job complete. Total processed: {processed}, Patched: {patched}, Not matching criteria: {notMatchingCriteria}, Errors: {errors}",
                request.TotalProcessed,
                request.TotalPatched,
                request.TotalNotMatchingCriteria,
                request.TotalErrors),
            BuildProgressMetrics = request => new Dictionary<string, object?>
            {
                ["windowSize"] = request.windowSize,
                ["totalProcessed"] = request.TotalProcessed,
                ["totalPatched"] = request.TotalPatched,
                ["totalNotMatchingCriteria"] = request.TotalNotMatchingCriteria,
                ["totalErrors"] = request.TotalErrors,
            },
        };

    private async Task<ChainedBatchJobFetchResult<CorrespondenceEntity>> FetchBatchAsync(
        UpdateOldCorrespondencesWithDownloadAllRequest request,
        CancellationToken cancellationToken)
    {
        var window = await correspondenceRepository.GetCorrespondencesWindowAfter(
            request.windowSize + 1,
            request.CursorCreated,
            request.CursorId,
            false,
            cancellationToken);

        var hasMore = window.Count > request.windowSize;
        if (hasMore)
        {
            window.RemoveAt(window.Count - 1);
        }

        return new ChainedBatchJobFetchResult<CorrespondenceEntity>(window, hasMore);
    }

    private async Task<UpdateOldCorrespondencesWithDownloadAllRequest> ProcessBatchAsync(
        UpdateOldCorrespondencesWithDownloadAllRequest request,
        IReadOnlyList<CorrespondenceEntity> correspondences,
        CancellationToken cancellationToken)
    {
        var batchProcessed = 0;
        var batchPatched = 0;
        var batchNotMatchingCriteria = 0;
        var batchErrors = 0;

        foreach (var correspondence in correspondences)
        {
            try
            {
                batchProcessed++;
                var attachments = await attachmentRepository.GetAttachmentsByCorrespondence(correspondence.Id, cancellationToken);
                if (attachments is { Count: >= 2 } && attachments.Sum(a => a.AttachmentSize) <= MaxTotalAttachmentSizeBytes)
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(
                        ChainedBatchJobQueues.Worker,
                        service => service.TryAddDownloadAllAttachmentsToDialog(correspondence.Id, CancellationToken.None));
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
                logger.LogError(ex, "Error processing correspondence {correspondenceId}", correspondence.Id);
            }
        }

        logger.LogInformation(
            "Batch complete. Processed: {processed}, Patched: {patched}, Not matching criteria: {notMatchingCriteria}, Errors: {errors}",
            batchProcessed,
            batchPatched,
            batchNotMatchingCriteria,
            batchErrors);

        return new UpdateOldCorrespondencesWithDownloadAllRequest
        {
            windowSize = request.windowSize,
            CursorCreated = request.CursorCreated,
            CursorId = request.CursorId,
            TotalProcessed = request.TotalProcessed + batchProcessed,
            TotalPatched = request.TotalPatched + batchPatched,
            TotalNotMatchingCriteria = request.TotalNotMatchingCriteria + batchNotMatchingCriteria,
            TotalErrors = request.TotalErrors + batchErrors,
        };
    }
}
