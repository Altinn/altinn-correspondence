using OneOf;
using System.Security.Claims;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.ManualRetryNotPublishedCorrespondences;

public class ManualRetryNotPublishedCorrespondencesHandler(
    IBackgroundJobClient backgroundJobClient,
    ILogger<ManualRetryNotPublishedCorrespondencesHandler> logger) : IHandler<ManualRetryNotPublishedCorrespondencesRequest, ManualRetryNotPublishedCorrespondencesResponse>

{
    public async Task<OneOf<ManualRetryNotPublishedCorrespondencesResponse, Error>> Process(ManualRetryNotPublishedCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var response = new ManualRetryNotPublishedCorrespondencesResponse();
        foreach (var correspondenceId in request.CorrespondenceIds)
        {
            logger.LogInformation("Retrying publish for correspondence {CorrespondenceId}", correspondenceId);
            var jobId = backgroundJobClient.Enqueue<PublishCorrespondenceHandler>(handler => handler.Process(correspondenceId, null, CancellationToken.None));
            response.RetriedCorrespondenceIds[correspondenceId] = $"Enqueued (jobId: {jobId})";
        }
        return response;
    }
}