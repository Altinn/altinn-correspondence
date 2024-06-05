using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;

namespace Altinn.Correspondence.Application.PurgeAttachmentCommand;

public class PurgeAttachmentCommandHandler(IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IStorageRepository storageRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, ICorrespondenceAttachmentRepository correspondenceAttachmentRepository, IEventBus eventBus) : IHandler<Guid, Guid>
{
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository = attachmentStatusRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository = correspondenceStatusRepository;
    private readonly ICorrespondenceAttachmentRepository _correspondenceAttachmentRepository = correspondenceAttachmentRepository;
    private readonly IStorageRepository _storageRepository = storageRepository;
    private readonly IEventBus _eventBus = eventBus;

    public async Task<OneOf<Guid, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        if (attachment.Statuses.Any(status => status.Status == AttachmentStatus.Purged))
        {
            return Errors.InvalidPurgeAttachmentStatus;
        }

        var correspondences = await _correspondenceRepository.GetCorrespondencesByAttachmentId(attachmentId, true, cancellationToken);

        if (correspondences.Count == 0)
        {
            await _storageRepository.PurgeAttachment(attachmentId, cancellationToken);
        }
        else if (attachment.Statuses.OrderByDescending(status => status.StatusChanged).First().Status != AttachmentStatus.Initialized &&
                !correspondences.All(correspondence => correspondence.Statuses.OrderByDescending(status => status.StatusChanged).First().Status == CorrespondenceStatus.Initialized))
        {

            if (correspondences.Any(correspondence => !(correspondence.Statuses.OrderByDescending(status => status.StatusChanged).First().Status == CorrespondenceStatus.PurgedByRecipient) && !(correspondence.Statuses.OrderByDescending(status => status.StatusChanged).First().Status == CorrespondenceStatus.PurgedByAltinn)))
            {
                return Errors.PurgeAttachmentWithExistingCorrespondence;
            }
            await _storageRepository.PurgeAttachment(attachmentId, cancellationToken);
        }
        else
        {
            await _correspondenceAttachmentRepository.PurgeCorrespondenceAttachmentsByAttachmentId(attachmentId, cancellationToken);
            await _storageRepository.PurgeAttachment(attachmentId, cancellationToken);
        }

        await _attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
        {
            AttachmentId = attachmentId,
            Status = AttachmentStatus.Purged,
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = AttachmentStatus.Purged.ToString()
        }, cancellationToken);

        await _eventBus.Publish(AltinnEventType.AttachmentPurged, null, attachmentId.ToString(), "attachment", null, cancellationToken);

        return attachmentId;
    }

    public async Task CheckCorrespondenceStatusesAfterDeleteAndPublish(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return;
        }

        var correspondences = await _correspondenceRepository.GetNonPublishedCorrespondencesByAttachmentId(attachment.Id, AttachmentStatus.Published, cancellationToken);
        if (correspondences.Count == 0)
        {
            return;
        }

        var list = new List<CorrespondenceStatusEntity>();
        foreach (var correspondence in correspondences)
        {
            list.Add(
                new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondence.Id,
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
