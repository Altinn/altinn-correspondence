using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.LegacyUpdateCorrespondenceStatus;
public class LegacyUpdateCorrespondenceStatusHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    UserClaimsHelper userClaimsHelper,
    IBackgroundJobClient backgroundJobClient,
    ILogger<LegacyUpdateCorrespondenceStatusHandler> logger) : IHandler<LegacyUpdateCorrespondenceStatusRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(LegacyUpdateCorrespondenceStatusRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var operationTimestamp = DateTimeOffset.UtcNow;
        if (userClaimsHelper.GetPartyId() is not int partyId)
        {
            return AuthorizationErrors.InvalidPartyId;
        }
        var party = await altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (party is null || (string.IsNullOrEmpty(party.SSN) && string.IsNullOrEmpty(party.OrgNumber)))
        {
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, false, cancellationToken, true);
        if (correspondence == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(
            user,
            party.SSN,
            correspondence.ResourceId,
            new List<ResourceAccessLevel> { ResourceAccessLevel.Read },
            correspondence.Recipient,
            cancellationToken);
        if (minimumAuthLevel == null)
        {
            return AuthorizationErrors.LegacyNoAccessToCorrespondence;
        }
        var currentStatusError = ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            return currentStatusError;
        }
        var updateError = ValidateUpdateRequest(request, correspondence);
        if (updateError is not null)
        {
            return updateError;
        }
        if (party.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondence.Id,
                Status = request.Status,
                StatusChanged = operationTimestamp,
                StatusText = request.Status.ToString(),
                PartyUuid = partyUuid
            }, cancellationToken);
            if (request.Status == CorrespondenceStatus.Confirmed)
            {
                var patchJobId = backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.PatchCorrespondenceDialogToConfirmed(correspondence.Id));
                backgroundJobClient.ContinueJobWith<IDialogportenService>(patchJobId, (dialogportenService) => dialogportenService.CreateConfirmedActivity(request.CorrespondenceId, DialogportenActorType.Recipient, operationTimestamp), JobContinuationOptions.OnlyOnSucceededState);
                backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
            } 
            else if (request.Status == CorrespondenceStatus.Read)
            {
                backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
                backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateOpenedActivity(correspondence.Id, DialogportenActorType.Recipient, operationTimestamp));
            }
            return request.CorrespondenceId;
        }, logger, cancellationToken);
    }

    private Error? ValidateUpdateRequest(LegacyUpdateCorrespondenceStatusRequest request, CorrespondenceEntity correspondence)
    {
        if (request.Status == CorrespondenceStatus.Read && !correspondence.StatusHasBeen(CorrespondenceStatus.Fetched))
        {
            return CorrespondenceErrors.ReadBeforeFetched;
        }
        if (request.Status == CorrespondenceStatus.Confirmed && !correspondence.StatusHasBeen(CorrespondenceStatus.Fetched))
        {
            return CorrespondenceErrors.ConfirmBeforeFetched;
        }
        if (request.Status == CorrespondenceStatus.Archived && correspondence.IsConfirmationNeeded is true && !correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
        {
            return CorrespondenceErrors.ArchiveBeforeConfirmed;
        }
        return null;
    }

    public Error? ValidateCurrentStatus(CorrespondenceEntity correspondence)
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
}