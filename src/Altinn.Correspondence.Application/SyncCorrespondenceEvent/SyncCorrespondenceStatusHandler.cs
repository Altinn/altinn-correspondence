using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceStatusEventHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper,
    ILogger<SyncCorrespondenceStatusEventHandler> logger) : IHandler<SyncCorrespondenceStatusEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceStatusEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing status Sync request for correspondence {CorrespondenceId} to status {Status}", 
            request.CorrespondenceId, 
            request.SyncedEvent.Status);
        var operationTimestamp = DateTimeOffset.UtcNow;
        
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        // TODO: Change validation to handle IDempotent Key instead of current status.==
        
        ValidateCurrentStatus(correspondence);



        logger.LogInformation("Executing status update transaction for correspondence {CorrespondenceId}", request.CorrespondenceId);
        await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            await updateCorrespondenceStatusHelper.AddCorrespondenceStatus(correspondence, request.SyncedEvent.Status, request.SyncedEvent.PartyUuid, cancellationToken);

            if (correspondence.IsMigrating == false)
            {
                updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, request.SyncedEvent.Status, operationTimestamp);
                updateCorrespondenceStatusHelper.PatchCorrespondenceDialog(request.CorrespondenceId, request.SyncedEvent.Status);
                updateCorrespondenceStatusHelper.PublishEvent(correspondence, request.SyncedEvent.Status);
            }

            return Task.CompletedTask;
        }, logger, cancellationToken);

        logger.LogInformation("Successfully synced status to {Status} for correspondence {CorrespondenceId}", 
            request.SyncedEvent.Status, 
            request.CorrespondenceId);
        return request.CorrespondenceId;
    }

    // <summary>
    /// Validates if the status of the synced correspondence allows for status updates.
    /// </summary>
    /// <param name="correspondence">The correspondence entity to validate</param>
    /// <returns></returns>
    private Error? ValidateCurrentStatus(CorrespondenceEntity correspondence)
    {
        var currentStatus = correspondence.GetHighestStatus();
        if (currentStatus is null)
        {
            return CorrespondenceErrors.CouldNotRetrieveStatus;
        }
        if (currentStatus!.Status.IsPurged())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        return null;
    }
}
