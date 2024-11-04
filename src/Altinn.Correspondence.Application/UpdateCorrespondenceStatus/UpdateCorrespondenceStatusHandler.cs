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
        var updateError = ValidateUpdateRequest(request, correspondence);
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
        await PublishEvent(correspondence, request.Status, cancellationToken);
        return request.CorrespondenceId;
    }

    private static Error? ValidateUpdateRequest(UpdateCorrespondenceStatusRequest request, CorrespondenceEntity correspondence)
    {
        if (request.Status == CorrespondenceStatus.Read && !correspondence.StatusHasBeen(CorrespondenceStatus.Fetched))
        {
            return Errors.ReadBeforeFetched;
        }
        if (request.Status == CorrespondenceStatus.Confirmed && !correspondence.StatusHasBeen(CorrespondenceStatus.Fetched))
        {
            return Errors.ConfirmBeforeFetched;
        }
        if (request.Status == CorrespondenceStatus.Archived && correspondence.IsConfirmationNeeded is true && !correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
        {
            return Errors.ArchiveBeforeConfirmed;
        }
        return null;
    }

    private async Task PublishEvent(CorrespondenceEntity correspondence, CorrespondenceStatus status, CancellationToken cancellationToken)
    {
        if (status == CorrespondenceStatus.Confirmed)
        {
            await _eventBus.Publish(AltinnEventType.CorrespondenceReceiverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        }
        else if (status == CorrespondenceStatus.Read)
        {
            await _eventBus.Publish(AltinnEventType.CorrespondenceReceiverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        }
    }

    // Must be public to be run by Hangfire
    public Task ReportActivityToDialogporten(Guid correspondenceId, DialogportenActorType dialogportenActorType, CorrespondenceStatus status) => status switch
    {
        CorrespondenceStatus.Confirmed => _dialogportenService.CreateInformationActivity(correspondenceId, dialogportenActorType, DialogportenTextType.CorrespondenceConfirmed),
        CorrespondenceStatus.Archived => _dialogportenService.CreateInformationActivity(correspondenceId, dialogportenActorType, DialogportenTextType.CorrespondenceArchived),
        _ => Task.CompletedTask
    };
}
