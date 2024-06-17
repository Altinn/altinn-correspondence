using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.RemoveCorrespondenceAttachment;

public class RemoveCorrespondenceAttachmentHandler : IHandler<RemoveCorrespondenceAttachmentRequest, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceAttachmentRepository _correspondenceAttachmentRepository;

    public RemoveCorrespondenceAttachmentHandler(ICorrespondenceRepository correspondenceRepository, ICorrespondenceAttachmentRepository correspondenceAttachmentRepository)
    {
        _correspondenceRepository = correspondenceRepository;
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
        var correspondenceAttachment = correspondence.Content?.Attachments.FirstOrDefault(a => a.AttachmentId == request.AttachmentId);
        if (correspondenceAttachment is null)
        {
            return Errors.CorrespondenceAttachmentNotFound;
        }

        return await _correspondenceAttachmentRepository.RemoveAttachmentFromCorrespondence(request.CorrespondenceId, request.AttachmentId);
    }
}
