using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeAttachmentCommand;

public class InitializeAttachmentCommandHandler : IHandler<InitializeAttachmentCommandRequest, int>
{

    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    public InitializeAttachmentCommandHandler(IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository)
    {
        _attachmentRepository = attachmentRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
    }

    public async Task<OneOf<int, Error>> Process(InitializeAttachmentCommandRequest request, CancellationToken cancellationToken)
    {
        var attachmentId = await _attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);
        var status = new AttachmentStatusEntity
        {
            AttachmentId = attachmentId,
            StatusChanged = DateTimeOffset.UtcNow,
            Status = Core.Models.Enums.AttachmentStatus.Initialized
        };
        await _attachmentStatusRepository.AddAttachmentStatus(status, cancellationToken);
        return attachmentId;
    }
}
