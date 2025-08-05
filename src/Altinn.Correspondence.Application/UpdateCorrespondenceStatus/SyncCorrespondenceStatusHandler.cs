using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;

public class SyncCorrespondenceStatusHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper,
    ILogger<SyncCorrespondenceStatusHandler> logger) : IHandler<SyncCorrespondenceStatusRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceStatusRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing status Sync request for correspondence {CorrespondenceId} to status {Status}", 
            request.CorrespondenceId, 
            request.Status);
        var operationTimestamp = DateTimeOffset.UtcNow;
        
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        // TODO: Change validation to handle IDempotent Key instead of current status.
        var currentStatusError = updateCorrespondenceStatusHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            logger.LogWarning("Current status validation failed for correspondence {CorrespondenceId}: {Error}", 
                request.CorrespondenceId, 
                currentStatusError);
            return currentStatusError;
        }
        var updateError = updateCorrespondenceStatusHelper.ValidateUpdateRequestStatus(request.Status, correspondence);
        if (updateError is not null)
        {
            logger.LogWarning("Update request validation failed for correspondence {CorrespondenceId}: {Error}", 
                request.CorrespondenceId, 
                updateError);
            return updateError;
        }

        logger.LogInformation("Executing status update transaction for correspondence {CorrespondenceId}", request.CorrespondenceId);
        await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            await updateCorrespondenceStatusHelper.AddCorrespondenceStatus(correspondence, request.Status, request.PartyUuid, cancellationToken);

            if (correspondence.IsMigrating == false)
            {
                updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, request.Status, operationTimestamp);
                updateCorrespondenceStatusHelper.PatchCorrespondenceDialog(request.CorrespondenceId, request.Status);
                updateCorrespondenceStatusHelper.PublishEvent(correspondence, request.Status);
            }

            return Task.CompletedTask;
        }, logger, cancellationToken);

        logger.LogInformation("Successfully updated status to {Status} for correspondence {CorrespondenceId}", 
            request.Status, 
            request.CorrespondenceId);
        return request.CorrespondenceId;
    }
}
