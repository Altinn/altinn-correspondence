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

    public PurgeCorrespondenceHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IStorageRepository storageRepository, IAttachmentStatusRepository attachmentStatusRepository, IEventBus eventBus, UserClaimsHelper userClaimsHelper, IBackgroundJobClient backgroundJobClient)
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

        if (correspondence.Statuses.Any(status => status.Status.IsPurged()))
        {
            return Errors.CorrespondenceAlreadyPurged;
        }

        var latestStatus = correspondence.GetLatestStatus();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var newStatus = new CorrespondenceStatusEntity();

        if (_userClaimsHelper.IsSender(correspondence.Sender))
        {
            if (!latestStatus.Status.IsPurgeableForSender())
            {
                return Errors.CantPurgeCorrespondenceSender;
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
            if (!latestStatus.Status.IsAvailableForRecipient())
            {
                return Errors.CorrespondenceNotFound;
            }
            newStatus = new CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.PurgedByRecipient,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = CorrespondenceStatus.PurgedByRecipient.ToString()
            };
        }

        await _eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondenceId.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        await _correspondenceStatusRepository.AddCorrespondenceStatus(newStatus, cancellationToken);
        await CheckAndPurgeAttachments(correspondenceId, cancellationToken);
        _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, newStatus.Status == CorrespondenceStatus.PurgedByRecipient ? DialogportenActorType.Recipient : DialogportenActorType.Sender, DialogportenTextType.CorrespondencePurged, (newStatus.Status == CorrespondenceStatus.PurgedByRecipient ? "mottaker" : "avsender")));
        _backgroundJobClient.Enqueue<CancelNotificationHandler>(handler => handler.Process(null, correspondenceId, cancellationToken));
        return correspondenceId;
    }

    public async Task CheckAndPurgeAttachments(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var attachments = await _attachmentRepository.GetAttachmentsByCorrespondence(correspondenceId, cancellationToken);
        foreach (var attachment in attachments)
        {
            var canBeDeleted = await _attachmentRepository.CanAttachmentBeDeleted(attachment.Id, cancellationToken);
            if (!canBeDeleted || attachment.Statuses.Any(status => status.Status == AttachmentStatus.Purged))
            {
                continue;
            }

            await _storageRepository.PurgeAttachment(attachment.Id, cancellationToken);
            var attachmentStatus = new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                Status = AttachmentStatus.Purged,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = AttachmentStatus.Purged.ToString()
            };
            await _attachmentStatusRepository.AddAttachmentStatus(attachmentStatus, cancellationToken);
        }
    }
}
