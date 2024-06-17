using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.AddCorrespondenceAttachment;

public class AddCorrespondenceAttachmentHandler : IHandler<AddCorrespondenceAttachmentRequest, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ICorrespondenceAttachmentRepository _correspondenceAttachmentRepository;
    public AddCorrespondenceAttachmentHandler(ICorrespondenceRepository correspondenceRepository, IAttachmentRepository attachmentRepository, ICorrespondenceAttachmentRepository correspondenceAttachmentRepository)
    {
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
        _correspondenceAttachmentRepository = correspondenceAttachmentRepository;
    }

    public async Task<OneOf<Guid, Error>> Process(AddCorrespondenceAttachmentRequest request, CancellationToken cancellationToken)
    {
        // TODO: Should validate that caller is part of correspondence
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, cancellationToken);
        if (correspondence is null)
        {
            return Errors.CorrespondenceNotFound;
        }
        if (correspondence.Statuses.Last().Status != Core.Models.Enums.CorrespondenceStatus.Initialized)
        {
            return Errors.CorrespondenceNotOpenForAttachments;
        }
        var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment is null)
        {
            return Errors.AttachmentNotFound;
        }
        if (attachment.Statuses.Last().Status != Core.Models.Enums.AttachmentStatus.Published)
        {
            return Errors.AttachmentNotPublished;
        }

        return await _correspondenceAttachmentRepository.AddAttachmentToCorrespondence(request.CorrespondenceId, request.AttachmentId, cancellationToken);
    }
}
