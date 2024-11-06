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
    private readonly IDialogportenService _dialogportenService;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public UpdateCorrespondenceStatusHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IEventBus eventBus, IDialogportenService dialogportenService, UserClaimsHelper userClaimsHelper, IBackgroundJobClient backgroundJobClient)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _eventBus = eventBus;
        _dialogportenService = dialogportenService;
        _userClaimsHelper = userClaimsHelper;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusRequest request, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var isRecipient = _userClaimsHelper.IsRecipient(correspondence.Recipient);
        if (!isRecipient)
        {
            return Errors.CorrespondenceNotFound;
        }
        var currentStatus = correspondence.GetLatestStatus();
        if (currentStatus is null)
        {
            return Errors.LatestStatusIsNull;
        }
        if (!currentStatus.Status.IsAvailableForRecipient())
        {
            return Errors.CorrespondenceNotFound;
        }
        if (currentStatus!.Status.IsPurged())
        {
            return Errors.CorrespondencePurged;
        }
        if (request.Status == CorrespondenceStatus.Read && correspondence.MarkedUnread == true)
        {
            await _correspondenceRepository.UpdateMarkedUnread(request.CorrespondenceId, false, cancellationToken);
        }
        var updateStatusHelper = new UpdateCorrespondenceStatusHelper();
        var updateError = updateStatusHelper.ValidateUpdateRequest(request, correspondence);
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
        _backgroundJobClient.Enqueue(() => ReportActivityToDialogporten(request.CorrespondenceId, DialogportenActorType.Recipient, request.Status));
        await updateStatusHelper.PublishEvent(_eventBus, correspondence, request.Status, cancellationToken);
        return request.CorrespondenceId;
    }

    // Must be public to be run by Hangfire
    public Task ReportActivityToDialogporten(Guid correspondenceId, DialogportenActorType dialogportenActorType, CorrespondenceStatus status) => status switch
    {
        CorrespondenceStatus.Confirmed => _dialogportenService.CreateInformationActivity(correspondenceId, dialogportenActorType, DialogportenTextType.CorrespondenceConfirmed),
        CorrespondenceStatus.Archived => _dialogportenService.CreateInformationActivity(correspondenceId, dialogportenActorType, DialogportenTextType.CorrespondenceArchived),
        _ => Task.CompletedTask
    };
}
