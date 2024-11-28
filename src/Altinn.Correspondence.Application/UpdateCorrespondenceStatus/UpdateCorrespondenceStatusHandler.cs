using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
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
    IEventBus eventBus,
    UserClaimsHelper userClaimsHelper,
    UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper,
    ILogger<UpdateCorrespondenceStatusHandler> logger) : IHandler<UpdateCorrespondenceStatusRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        string? onBehalfOf = request.OnBehalfOf;
        bool isOnBehalfOfRecipient = false;
        if (!string.IsNullOrEmpty(onBehalfOf))
        {
            isOnBehalfOfRecipient = correspondence.Recipient.WithoutPrefix() == onBehalfOf.WithoutPrefix();
        }
        var hasAccess = await altinnAuthorizationService.CheckUserAccess(
            user,
            correspondence.ResourceId,
            request.OnBehalfOf ?? correspondence.Recipient,
            correspondence.Id.ToString(),
            [ResourceAccessLevel.Read],
            cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var isRecipient = userClaimsHelper.IsRecipient(correspondence.Recipient) || isOnBehalfOfRecipient;
        if (!isRecipient)
        {
            return Errors.CorrespondenceNotFound;
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
        var party = await altinnRegisterService.LookUpPartyById(userClaimsHelper.GetUserID(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return Errors.CouldNotFindPartyUuid;
        }
        
        await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            await updateCorrespondenceStatusHelper.AddCorrespondenceStatus(correspondence, request.Status, partyUuid, cancellationToken);
            updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, request.Status);
            await updateCorrespondenceStatusHelper.PublishEvent(eventBus, correspondence, request.Status, cancellationToken);
            return Task.CompletedTask;
        },logger, cancellationToken);

        return request.CorrespondenceId;
    }
}
