using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
public class LegacyUpdateCorrespondenceStatusHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    UserClaimsHelper userClaimsHelper,
    UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper,
    ILogger<LegacyUpdateCorrespondenceStatusHandler> logger) : IHandler<UpdateCorrespondenceStatusRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
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
        var currentStatusError = updateCorrespondenceStatusHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            return currentStatusError;
        }
        var updateError = updateCorrespondenceStatusHelper.ValidateUpdateRequest(request, correspondence);
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
            await updateCorrespondenceStatusHelper.AddCorrespondenceStatus(correspondence, request.Status, partyUuid, cancellationToken);
            updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, request.Status, operationTimestamp);
            updateCorrespondenceStatusHelper.PublishEvent(correspondence, request.Status);
            return request.CorrespondenceId;
        }, logger, cancellationToken);
    }
}