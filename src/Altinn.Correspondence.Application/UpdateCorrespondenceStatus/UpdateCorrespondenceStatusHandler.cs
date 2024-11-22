using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;

public class UpdateCorrespondenceStatusHandler : IHandler<UpdateCorrespondenceStatusRequest, Guid>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IEventBus _eventBus;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly UpdateCorrespondenceStatusHelper _updateCorrespondenceStatusHelper;

    public UpdateCorrespondenceStatusHandler(
        IAltinnAuthorizationService altinnAuthorizationService,
        ICorrespondenceRepository correspondenceRepository,
        ICorrespondenceStatusRepository correspondenceStatusRepository,
        IEventBus eventBus,
        UserClaimsHelper userClaimsHelper,
        UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _eventBus = eventBus;
        _userClaimsHelper = userClaimsHelper;
        _updateCorrespondenceStatusHelper = updateCorrespondenceStatusHelper;
    }

    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
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
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(
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
        var isRecipient = _userClaimsHelper.IsRecipient(correspondence.Recipient) || isOnBehalfOfRecipient;
        if (!isRecipient)
        {
            return Errors.CorrespondenceNotFound;
        }
        var currentStatusError = _updateCorrespondenceStatusHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            return currentStatusError;
        }
        var updateError = _updateCorrespondenceStatusHelper.ValidateUpdateRequest(request, correspondence);
        if (updateError is not null)
        {
            return updateError;
        }
        await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
        {
            CorrespondenceId = request.CorrespondenceId,
            Status = request.Status,
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = request.Status.ToString(),
        }, cancellationToken);
        _updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, request.Status);
        await _updateCorrespondenceStatusHelper.PublishEvent(_eventBus, correspondence, request.Status, cancellationToken);
        return request.CorrespondenceId;
    }
}
