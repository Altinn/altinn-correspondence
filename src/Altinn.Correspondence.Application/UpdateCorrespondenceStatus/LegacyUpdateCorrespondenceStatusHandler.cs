using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
public class LegacyUpdateCorrespondenceStatusHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    IEventBus eventBus,
    UserClaimsHelper userClaimsHelper,
    UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper,
    ILogger<LegacyUpdateCorrespondenceStatusHandler> logger) : IHandler<UpdateCorrespondenceStatusRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (userClaimsHelper.GetPartyId() is not int partyId)
        {
            return Errors.InvalidPartyId;
        }
        var party = await altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (party is null || (string.IsNullOrEmpty(party.SSN) && string.IsNullOrEmpty(party.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo;
        }
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
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
            return Errors.LegacyNoAccessToCorrespondence;
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
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondence.Id,
                Status = request.Status,
                StatusChanged = DateTime.UtcNow,
                StatusText = request.Status.ToString(),
            }, cancellationToken);

            updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, request.Status);
            await updateCorrespondenceStatusHelper.PublishEvent(eventBus, correspondence, request.Status, cancellationToken);
            return request.CorrespondenceId;
        }, logger, cancellationToken);
    }
}