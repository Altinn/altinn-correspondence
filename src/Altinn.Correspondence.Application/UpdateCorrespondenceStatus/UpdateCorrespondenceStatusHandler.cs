using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;

public class UpdateCorrespondenceStatusHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
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
            isOnBehalfOfRecipient = correspondence.Recipient.GetOrgNumberWithoutPrefix() == onBehalfOf.GetOrgNumberWithoutPrefix();
        }
        var hasAccess = await altinnAuthorizationService.CheckUserAccess(
            user,
            correspondence.ResourceId,
            [ResourceAccessLevel.Read],
            cancellationToken,
            onBehalfOf: isOnBehalfOfRecipient ? onBehalfOf : null,
            correspondenceId: isOnBehalfOfRecipient ? request.CorrespondenceId.ToString() : null);
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
        
        await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = request.CorrespondenceId,
                Status = request.Status,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = request.Status.ToString(),
            }, cancellationToken);
            updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, request.Status);
            await updateCorrespondenceStatusHelper.PublishEvent(eventBus, correspondence, request.Status, cancellationToken);
            return Task.CompletedTask;
        },logger, cancellationToken);

        return request.CorrespondenceId;
    }
}
