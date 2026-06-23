using System.Security.Claims;
using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Common.Helpers;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.UpdateOldCorrespondencesWithDownloadAll;

public class UpdateOldCorrespondencesWithDownloadAllHandler(
    IBackgroundJobClient backgroundJobClient,
    ChainedBatchJobOrchestrator chainedBatchJobOrchestrator,
    UpdateOldCorrespondencesWithDownloadAllBatchJob updateOldCorrespondencesWithDownloadAllBatchJob,
    ILogger<UpdateOldCorrespondencesWithDownloadAllHandler> logger) : IHandler<UpdateOldCorrespondencesWithDownloadAllRequest, UpdateOldCorrespondencesWithDownloadAllResponse>
{
    public Task<OneOf<UpdateOldCorrespondencesWithDownloadAllResponse, Error>> Process(UpdateOldCorrespondencesWithDownloadAllRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting update of old correspondences with download all. Window size: {windowSize}", request.windowSize);
        var jobId = backgroundJobClient.Enqueue<UpdateOldCorrespondencesWithDownloadAllHandler>(
            ChainedBatchJobQueues.Orchestrator,
            handler => handler.ExecutePatchingInBackground(request, CancellationToken.None));

        logger.LogInformation("Orchestrator job {jobId} has been enqueued", jobId);

        return Task.FromResult<OneOf<UpdateOldCorrespondencesWithDownloadAllResponse, Error>>(new UpdateOldCorrespondencesWithDownloadAllResponse
        {
            JobId = jobId,
            Message = "Cleanup job has been Enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecutePatchingInBackground(UpdateOldCorrespondencesWithDownloadAllRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing batch starting after cursor {cursorId}", request.CursorId?.ToString().SanitizeForLogging());
        await chainedBatchJobOrchestrator.RunBatchAsync(
            request,
            updateOldCorrespondencesWithDownloadAllBatchJob.CreateDefinition(),
            cancellationToken);
    }
}
