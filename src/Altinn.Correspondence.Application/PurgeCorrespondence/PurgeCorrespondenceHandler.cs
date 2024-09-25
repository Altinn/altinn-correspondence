using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Altinn.Notifications;
using OneOf;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;

public class PurgeCorrespondenceHandler : IHandler<Guid, Guid>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnNotificationService _altinnNotificationService;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IStorageRepository _storageRepository;
    private readonly IEventBus _eventBus;
    private readonly UserClaimsHelper _userClaimsHelper;

    public PurgeCorrespondenceHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IStorageRepository storageRepository, IAttachmentStatusRepository attachmentStatusRepository, IEventBus eventBus, UserClaimsHelper userClaimsHelper, IAltinnNotificationService altinnNotificationService)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnNotificationService = altinnNotificationService;
        _attachmentRepository = attachmentRepository;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _storageRepository = storageRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
        _eventBus = eventBus;
        _userClaimsHelper = userClaimsHelper;
    }

    public async Task<OneOf<Guid, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null) return Errors.CorrespondenceNotFound;
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Open }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        if (correspondence.Statuses.Any(status => status.Status.IsPurged()))
        {
            return Errors.CorrespondenceAlreadyPurged;
        }

        string orgNo = _userClaimsHelper.GetUserID();
        if (orgNo is null)
        {
            return Errors.CouldNotFindOrgNo;
        }

        var latestStatus = correspondence.GetLatestStatus();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var newStatus = new CorrespondenceStatusEntity();

        if (correspondence.Sender == orgNo)
        {
            if (latestStatus.Status >= CorrespondenceStatus.Published && latestStatus.Status != CorrespondenceStatus.Failed)
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
        else if (correspondence.Recipient == orgNo)
        {
            if (latestStatus.Status < CorrespondenceStatus.Published)
            {
                return Errors.CantPurgeCorrespondenceRecipient;
            }
            newStatus = new CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.PurgedByRecipient,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = CorrespondenceStatus.PurgedByRecipient.ToString()
            };
        }
        else
        {
            return Errors.CantPurgeCorrespondence;
        }

        await _eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondenceId.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        await _correspondenceStatusRepository.AddCorrespondenceStatus(newStatus, cancellationToken);
        await CheckAndPurgeAttachments(correspondenceId, cancellationToken);
        foreach (var notification in correspondence.Notifications)
        {
            if (notification.RequestedSendTime > DateTimeOffset.UtcNow && notification.NotificationOrderId != null) await _altinnNotificationService.CancelNotification(notification.NotificationOrderId.ToString(), cancellationToken);
        }
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
