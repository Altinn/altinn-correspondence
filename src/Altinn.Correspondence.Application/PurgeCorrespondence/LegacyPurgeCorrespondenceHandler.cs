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

namespace Altinn.Correspondence.Application.PurgeCorrespondence;
public class LegacyPurgeCorrespondenceHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    IEventBus eventBus,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper,
    UserClaimsHelper userClaimsHelper,
    IBackgroundJobClient backgroundJobClient,
    ILogger<LegacyPurgeCorrespondenceHandler> logger) : IHandler<Guid, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (userClaimsHelper.GetPartyId() is not int partyId)
        {
            return AuthorizationErrors.InvalidPartyId;
        }
        var party = await altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (party is null || (string.IsNullOrEmpty(party.SSN) && string.IsNullOrEmpty(party.OrgNumber)))
        {
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(user, party.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
        if (minimumAuthLevel == null)
        {
            return AuthorizationErrors.LegacyNoAccessToCorrespondence;
        }
        var recipientPurgeError = purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence, true);
        if (recipientPurgeError is not null)
        {
            return recipientPurgeError;
        }
        if (party.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.PurgedByRecipient,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = CorrespondenceStatus.PurgedByRecipient.ToString(),
                PartyUuid = partyUuid,
            }, cancellationToken);

            backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondenceId.ToString(), "correspondence", correspondence.Sender, cancellationToken));
            await purgeCorrespondenceHelper.CheckAndPurgeAttachments(correspondenceId, partyUuid, cancellationToken);
            purgeCorrespondenceHelper.ReportActivityToDialogporten(isSender: false, correspondenceId);
            purgeCorrespondenceHelper.CancelNotification(correspondenceId, cancellationToken);
            return correspondenceId;
        }, logger, cancellationToken);
    }
}