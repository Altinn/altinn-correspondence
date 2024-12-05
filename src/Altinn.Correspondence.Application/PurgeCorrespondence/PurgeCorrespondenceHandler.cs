using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;

public class PurgeCorrespondenceHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IEventBus eventBus,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper,
    ILogger<PurgeCorrespondenceHandler> logger) : IHandler<PurgeCorrespondenceRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(PurgeCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        Guid correspondenceId = request.CorrespondenceId;
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var hasAccessAsSender = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            correspondence,
            cancellationToken);
        var hasAccessAsRecipient = await altinnAuthorizationService.CheckAccessAsRecipient(
            user,
            correspondence,
            cancellationToken);
        if (!hasAccessAsSender && !hasAccessAsRecipient)
        {
            return AuthorizationErrors.NoAccessToResource;
        }
        if (hasAccessAsSender)
        {
            var senderRecipientPurgeError = purgeCorrespondenceHelper.ValidatePurgeRequestSender(correspondence);
            if (senderRecipientPurgeError is not null)
            {
                return senderRecipientPurgeError;
            }
        }
        else if (hasAccessAsRecipient)
        {
            var recipientPurgeError = purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence);
            if (recipientPurgeError is not null)
            {
                return recipientPurgeError;
            }
        }
        var currentStatusError = purgeCorrespondenceHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            return currentStatusError;
        }
        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            var status = hasAccessAsSender ? CorrespondenceStatus.PurgedByAltinn : CorrespondenceStatus.PurgedByRecipient;
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = status.ToString(),
                PartyUuid = partyUuid
            }, cancellationToken);

            await eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondenceId.ToString(), "correspondence", correspondence.Sender, cancellationToken);
            await purgeCorrespondenceHelper.CheckAndPurgeAttachments(correspondenceId, partyUuid, cancellationToken);
            purgeCorrespondenceHelper.ReportActivityToDialogporten(hasAccessAsSender && user.CallingAsSender(), correspondenceId);
            purgeCorrespondenceHelper.CancelNotification(correspondenceId, cancellationToken);
            return correspondenceId;
        }, logger, cancellationToken);
    }
}