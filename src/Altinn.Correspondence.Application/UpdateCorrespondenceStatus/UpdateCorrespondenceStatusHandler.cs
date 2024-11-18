using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using OneOf;

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

    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusRequest request, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var isRecipient = _userClaimsHelper.IsRecipient(correspondence.Recipient);
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(
            correspondence.ResourceId,
            new List<ResourceAccessLevel> { ResourceAccessLevel.Read },
            cancellationToken,
            onBehalfOf: isRecipient ? null : correspondence.Recipient,
            correspondenceId: isRecipient ? null : request.CorrespondenceId.ToString());
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
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
