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
using OpenTelemetry.Trace;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.MarkCorrespondenceAsRead;

public class MarkCorrespondenceAsReadHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IBackgroundJobClient backgroundJobClient,
    IEventBus eventBus,
    ILogger<MarkCorrespondenceAsReadHandler> logger) : IHandler<MarkCorrespondenceAsReadRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(MarkCorrespondenceAsReadRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing mark as read request for correspondence {CorrespondenceId}", 
            request.CorrespondenceId);
        
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
        
        var updateError = ValidateMarkAsReadRequest(correspondence);
        if (updateError is not null)
        {
            logger.LogWarning("Mark as read request validation failed for correspondence {CorrespondenceId}: {Error}", 
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

        logger.LogInformation("Executing mark as read transaction for correspondence {CorrespondenceId}", request.CorrespondenceId);
        await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            var operationTimestamp = DateTime.UtcNow;
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondence.Id,
                Status = CorrespondenceStatus.Read,
                StatusChanged = operationTimestamp,
                StatusText = CorrespondenceStatus.Read.ToString(),
                PartyUuid = partyUuid
            }, cancellationToken);
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
            if (correspondence.Altinn2CorrespondenceId.HasValue && correspondence.Altinn2CorrespondenceId > 0)
            {
                backgroundJobClient.Enqueue<IAltinnStorageService>(syncEventToAltinn2 => syncEventToAltinn2.SyncCorrespondenceEventToSblBridge(
                    correspondence.Altinn2CorrespondenceId.Value,
                    party.PartyId,
                    operationTimestamp,
                    SyncEventType.Read,
                    CancellationToken.None));
            }
            return Task.CompletedTask;
        }, logger, cancellationToken);

        logger.LogInformation("Successfully marked correspondence {CorrespondenceId} as read", 
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

    private Error? ValidateMarkAsReadRequest(CorrespondenceEntity correspondence)
    {
        if (!correspondence.StatusHasBeen(CorrespondenceStatus.Fetched))
        {
            return CorrespondenceErrors.ReadBeforeFetched;
        }
        return null;
    }
} 