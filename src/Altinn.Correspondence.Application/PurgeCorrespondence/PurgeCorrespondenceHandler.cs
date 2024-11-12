using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;
using Hangfire;
using Altinn.Correspondence.Application.CancelNotification;
using Altinn.Correspondence.Integrations.Altinn.Authorization;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;

public class PurgeCorrespondenceHandler : IHandler<Guid, Guid>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IStorageRepository _storageRepository;
    private readonly IEventBus _eventBus;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly PurgeCorrespondenceHelper _purgeCorrespondenceHelper;

    public PurgeCorrespondenceHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IStorageRepository storageRepository, IAttachmentStatusRepository attachmentStatusRepository, IEventBus eventBus, UserClaimsHelper userClaimsHelper, IBackgroundJobClient backgroundJobClient, PurgeCorrespondenceHelper purgeCorrespondenceHelper)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _attachmentRepository = attachmentRepository;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _storageRepository = storageRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
        _eventBus = eventBus;
        _userClaimsHelper = userClaimsHelper;
        _backgroundJobClient = backgroundJobClient;
        _purgeCorrespondenceHelper = purgeCorrespondenceHelper;
    }

    public async Task<OneOf<Guid, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        if (!_userClaimsHelper.IsAffiliatedWithCorrespondence(correspondence.Recipient, correspondence.Sender))
        {
            return Errors.CorrespondenceNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read, ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var currentStatusError = _purgeCorrespondenceHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            return currentStatusError;
        }

        var newStatus = new CorrespondenceStatusEntity();

        if (_userClaimsHelper.IsSender(correspondence.Sender))
        {
            var senderRecipientPurgeError = _purgeCorrespondenceHelper.ValidatePurgeRequestSender(correspondence);
            if (senderRecipientPurgeError is not null)
            {
                return senderRecipientPurgeError;
            }
            newStatus = new CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.PurgedByAltinn,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = CorrespondenceStatus.PurgedByAltinn.ToString()
            };
        }
        else if (_userClaimsHelper.IsRecipient(correspondence.Recipient))
        {
            var recipientPurgeError = _purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence);
            if (recipientPurgeError is not null)
            {
                return recipientPurgeError;
            }
            newStatus = new CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.PurgedByRecipient,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = CorrespondenceStatus.PurgedByRecipient.ToString()
            };
        }

        await _correspondenceStatusRepository.AddCorrespondenceStatus(newStatus, cancellationToken);
        await _eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondenceId.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        await _purgeCorrespondenceHelper.CheckAndPurgeAttachments(correspondenceId, _attachmentRepository, _storageRepository, _attachmentStatusRepository, cancellationToken);
        _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, newStatus.Status == CorrespondenceStatus.PurgedByRecipient ? DialogportenActorType.Recipient : DialogportenActorType.Sender, DialogportenTextType.CorrespondencePurged, newStatus.Status == CorrespondenceStatus.PurgedByRecipient ? "mottaker" : "avsender"));
        _backgroundJobClient.Enqueue<CancelNotificationHandler>(handler => handler.Process(null, correspondenceId, cancellationToken));
        return correspondenceId;
    }
}