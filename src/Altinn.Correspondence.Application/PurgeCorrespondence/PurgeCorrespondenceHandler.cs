using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;

public class PurgeCorrespondenceHandler : IHandler<Guid, Guid>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IStorageRepository _storageRepository;

    public PurgeCorrespondenceHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IStorageRepository storageRepository, IAttachmentStatusRepository attachmentStatusRepository)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _attachmentRepository = attachmentRepository;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _storageRepository = storageRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
    }

    public async Task<OneOf<Guid, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null) return Errors.AttachmentNotFound;
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Open }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        if (correspondence.Statuses.Any(status => status.Status == CorrespondenceStatus.PurgedByRecipient || status.Status == CorrespondenceStatus.PurgedByAltinn))
        {
            return Errors.CorrespondenceAlreadyPurged;
        }

        // TODO: sender should only be able to delete correspondence if it is not published
        // Receiver should be able to delete correspondence if it is published

        var newStatus = new CorrespondenceStatusEntity()
        {
            CorrespondenceId = correspondenceId,
            Status = CorrespondenceStatus.PurgedByRecipient, // Todo: select status based on user role
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = CorrespondenceStatus.PurgedByRecipient.ToString()
        };
        await _correspondenceStatusRepository.AddCorrespondenceStatus(newStatus, cancellationToken);
        await CheckAndPurgeAttachments(correspondenceId, cancellationToken);
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
