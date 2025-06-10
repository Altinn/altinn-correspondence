using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;

public class UpdateCorrespondenceStatusHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper,
    ILogger<UpdateCorrespondenceStatusHandler> logger) : IHandler<UpdateCorrespondenceStatusRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing status update request for correspondence {CorrespondenceId} to status {Status}", 
            request.CorrespondenceId, 
            request.Status);
        var operationTimestamp = DateTimeOffset.UtcNow;
        
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsRecipient(
            user,
            correspondence,
            cancellationToken);
        if (!hasAccess)
        {
            logger.LogWarning("Access denied for correspondence {CorrespondenceId} - user does not have recipient access", request.CorrespondenceId);
            return AuthorizationErrors.NoAccessToResource;
        }

        var currentStatusError = updateCorrespondenceStatusHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            logger.LogWarning("Current status validation failed for correspondence {CorrespondenceId}: {Error}", 
                request.CorrespondenceId, 
                currentStatusError);
            return currentStatusError;
        }
        var updateError = updateCorrespondenceStatusHelper.ValidateUpdateRequest(request, correspondence);
        if (updateError is not null)
        {
            logger.LogWarning("Update request validation failed for correspondence {CorrespondenceId}: {Error}", 
                request.CorrespondenceId, 
                updateError);
            return updateError;
        }
        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for organization {OrganizationId}", user.GetCallerOrganizationId());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        logger.LogInformation("Executing status update transaction for correspondence {CorrespondenceId}", request.CorrespondenceId);
        await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            await updateCorrespondenceStatusHelper.AddCorrespondenceStatus(correspondence, request.Status, partyUuid, cancellationToken);
            updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, request.Status, operationTimestamp);
            updateCorrespondenceStatusHelper.PatchCorrespondenceDialog(request.CorrespondenceId, request.Status);
            updateCorrespondenceStatusHelper.PublishEvent(correspondence, request.Status);

            return Task.CompletedTask;
        }, logger, cancellationToken);

        logger.LogInformation("Successfully updated status to {Status} for correspondence {CorrespondenceId}", 
            request.Status, 
            request.CorrespondenceId);
        return request.CorrespondenceId;
    }
}
