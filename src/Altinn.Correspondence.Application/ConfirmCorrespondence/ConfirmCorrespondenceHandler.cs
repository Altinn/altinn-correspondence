using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.ConfirmCorrespondence;

public class ConfirmCorrespondenceHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IBackgroundJobClient backgroundJobClient,
    IEventBus eventBus,
    IDialogportenService dialogportenService,
    ILogger<ConfirmCorrespondenceHandler> logger) : IHandler<ConfirmCorrespondenceRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(ConfirmCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing confirmation request for correspondence {CorrespondenceId}", 
            request.CorrespondenceId);
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

        var currentStatusError = ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            logger.LogWarning("Current status validation failed for correspondence {CorrespondenceId}: {Error}", 
                request.CorrespondenceId, 
                currentStatusError);
            return currentStatusError;
        }
        
        var updateError = ValidateConfirmRequest(correspondence);
        if (updateError is not null)
        {
            logger.LogWarning("Confirm request validation failed for correspondence {CorrespondenceId}: {Error}", 
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

        logger.LogInformation("Executing confirmation transaction for correspondence {CorrespondenceId}", request.CorrespondenceId);
        await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondence.Id,
                Status = CorrespondenceStatus.Confirmed,
                StatusChanged = DateTime.UtcNow,
                StatusText = CorrespondenceStatus.Confirmed.ToString(),
                PartyUuid = partyUuid
            }, cancellationToken);
            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateConfirmedActivity(correspondence.Id, DialogportenActorType.Recipient, operationTimestamp));
            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.PatchCorrespondenceDialogToConfirmed(correspondence.Id));
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
            return Task.CompletedTask;
        }, logger, cancellationToken);

        logger.LogInformation("Successfully confirmed correspondence {CorrespondenceId}", 
            request.CorrespondenceId);
        return request.CorrespondenceId;
    }

    private Error? ValidateCurrentStatus(CorrespondenceEntity correspondence)
    {
        var currentStatus = correspondence.GetHighestStatus();
        if (currentStatus is null)
        {
            return CorrespondenceErrors.CouldNotRetrieveStatus;
        }
        if (!currentStatus.Status.IsAvailableForRecipient())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (currentStatus!.Status.IsPurged())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        return null;
    }

    private Error? ValidateConfirmRequest(CorrespondenceEntity correspondence)
    {
        if (!correspondence.StatusHasBeen(CorrespondenceStatus.Fetched))
        {
            return CorrespondenceErrors.ConfirmBeforeFetched;
        }
        return null;
    }
} 