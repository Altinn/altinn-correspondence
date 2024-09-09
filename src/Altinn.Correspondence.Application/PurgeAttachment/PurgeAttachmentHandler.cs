using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;

namespace Altinn.Correspondence.Application.PurgeAttachment;

public class PurgeAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IStorageRepository storageRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IEventBus eventBus) : IHandler<Guid, Guid>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository = attachmentStatusRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository = correspondenceStatusRepository;
    private readonly IStorageRepository _storageRepository = storageRepository;
    private readonly IEventBus _eventBus = eventBus;

    public async Task<OneOf<Guid, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Open }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (attachment.Statuses.Any(status => status.Status == AttachmentStatus.Purged))
        {
            return Errors.InvalidPurgeAttachmentStatus;
        }

        var correspondences = await _correspondenceRepository.GetCorrespondencesByAttachmentId(attachmentId, true, cancellationToken);
        bool isCorrespondencePurged = correspondences
            .All(correspondence =>
            {
                var latestStatus = correspondence.Statuses
                    .OrderByDescending(status => status.StatusChanged)
                    .First().Status;

                return latestStatus == CorrespondenceStatus.PurgedByRecipient ||
                       latestStatus == CorrespondenceStatus.PurgedByAltinn;
            });
        if (correspondences.Count == 0 || isCorrespondencePurged)

        {
            await _storageRepository.PurgeAttachment(attachmentId, cancellationToken);
        }
        else
        {
            return Errors.PurgeAttachmentWithExistingCorrespondence;
        }

        await _attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
        {
            AttachmentId = attachmentId,
            Status = AttachmentStatus.Purged,
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = AttachmentStatus.Purged.ToString()
        }, cancellationToken);

        await _eventBus.Publish(AltinnEventType.AttachmentPurged, attachment.ResourceId, attachmentId.ToString(), "attachment", attachment.Sender, cancellationToken);

        return attachmentId;
    }

    public async Task CheckCorrespondenceStatusesAfterDeleteAndPublish(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return;
        }

        var correspondences = await _correspondenceRepository.GetNonPublishedCorrespondencesByAttachmentId(attachment.Id, cancellationToken);
        if (correspondences.Count == 0)
        {
            return;
        }

        var list = new List<CorrespondenceStatusEntity>();
        foreach (var correspondenceId in correspondences)
        {
            list.Add(
                new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Published,
                    StatusChanged = DateTime.UtcNow,
                    StatusText = CorrespondenceStatus.Published.ToString()
                }
            );
        }
        await _correspondenceStatusRepository.AddCorrespondenceStatuses(list, cancellationToken);
        return;
    }
}
