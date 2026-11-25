using OneOf;
using System.Security.Claims;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.ManualRetryNotPublishedCorrespondences;

public class ManualRetryNotPublishedCorrespondencesHandler(
    IBackgroundJobClient backgroundJobClient,
    ICorrespondenceRepository correspondenceRepository,
    ILogger<ManualRetryNotPublishedCorrespondencesHandler> logger) : IHandler<ManualRetryNotPublishedCorrespondencesRequest, ManualRetryNotPublishedCorrespondencesResponse>

{
    public async Task<OneOf<ManualRetryNotPublishedCorrespondencesResponse, Error>> Process(ManualRetryNotPublishedCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var response = new ManualRetryNotPublishedCorrespondencesResponse();
        foreach (var correspondenceId in request.CorrespondenceIds)
        {
            logger.LogInformation("Retrying publish for correspondence {CorrespondenceId}", correspondenceId);

            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, false, cancellationToken);
            if (correspondence is null)
            {
                response.RetriedCorrespondenceIds[correspondenceId] = "Not found";
                continue;
            }

            if (correspondence.StatusHasBeen(CorrespondenceStatus.Published) || correspondence.StatusHasBeen(CorrespondenceStatus.Failed) || correspondence.HasBeenPurged())
            {
                logger.LogInformation("Skipping retry for correspondence {CorrespondenceId} - already published, failed or purged", correspondenceId);
                response.RetriedCorrespondenceIds[correspondenceId] = "Already published, failed or purged";
                continue;
            }

            // Before the requested publish time the scheduled jobs have not run yet, and retrying would run them twice
            if (correspondence.RequestedPublishTime > DateTimeOffset.UtcNow)
            {
                logger.LogInformation("Skipping retry for correspondence {CorrespondenceId} - requested publish time has not passed", correspondenceId);
                response.RetriedCorrespondenceIds[correspondenceId] = "Requested publish time has not passed";
                continue;
            }

            var hasDialogportenReference = correspondence.ExternalReferences
                .Any(r => r.ReferenceType == ReferenceType.DialogportenDialogId);

            string jobId;
            if (!hasDialogportenReference)
            {
                var dialogJobId = backgroundJobClient.Enqueue<InitializeCorrespondencesHandler>(h => h.CreateDialogportenDialog(correspondenceId));
                jobId = backgroundJobClient.ContinueJobWith<PublishCorrespondenceHandler>(dialogJobId, handler => handler.Process(correspondenceId, null, CancellationToken.None), JobContinuationOptions.OnAnyFinishedState);
                response.RetriedCorrespondenceIds[correspondenceId] = $"Enqueued create dialog then publish (jobId: {jobId})";
            }
            else if (correspondence.IsTransmission() && !correspondence.HasCreatedTransmission())
            {
                jobId = backgroundJobClient.Enqueue<InitializeCorrespondencesHandler>(h => h.ScheduleTransmissionAndPublishJobs(correspondenceId, 0, correspondence.RequestedPublishTime, CancellationToken.None));
                response.RetriedCorrespondenceIds[correspondenceId] = $"Enqueued scheduling of transmission then publish (jobId: {jobId})";
            }
            else
            {
                jobId = backgroundJobClient.Enqueue<PublishCorrespondenceHandler>(handler => handler.Process(correspondenceId, null, CancellationToken.None));
                response.RetriedCorrespondenceIds[correspondenceId] = $"Enqueued (jobId: {jobId})";
            }
        }
        return response;
    }
}