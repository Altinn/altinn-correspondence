using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.RemoveCorrespondenceAttachment;

public class RemoveCorrespondenceAttachmentHandler : IHandler<RemoveCorrespondenceAttachmentRequest, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ICorrespondenceAttachmentRepository _correspondenceAttachmentRepository;

    public RemoveCorrespondenceAttachmentHandler(ICorrespondenceRepository correspondenceRepository, IAttachmentRepository attachmentRepository, ICorrespondenceAttachmentRepository correspondenceAttachmentRepository)
    {
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
        _correspondenceAttachmentRepository = correspondenceAttachmentRepository;
    }

    public async Task<OneOf<Guid, Error>> Process(RemoveCorrespondenceAttachmentRequest request, CancellationToken cancellationToken)
    {
        // TODO: Should validate that caller is part of correspondence
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, cancellationToken);
        if (correspondence is null)
        {
            return Errors.CorrespondenceNotFound;
        }
        if (correspondence.Statuses.Any(statusEntity => statusEntity.Status == Core.Models.Enums.CorrespondenceStatus.Published))
        {
            return Errors.CorrespondenceNotOpenForAttachments;
        }
        var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, false, cancellationToken);
        if (attachment is null)
        {
            return Errors.AttachmentNotFound;
        }

        var removedAttachment = await _correspondenceAttachmentRepository.RemoveAttachmentFromCorrespondence(request.CorrespondenceId, request.AttachmentId);

        if (removedAttachment == Guid.Empty) 
        { 
            return Errors.CorrespondenceAttachmentNotFound; 
        }
        return removedAttachment;
    }
}
