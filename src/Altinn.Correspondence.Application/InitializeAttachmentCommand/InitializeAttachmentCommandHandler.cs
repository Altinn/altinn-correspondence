using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeAttachmentCommand;

public class InitializeAttachmentCommandHandler : IHandler<InitializeAttachmentCommandRequest, int>
{

    private readonly IAttachmentRepository _attachmentRepository;
    public InitializeAttachmentCommandHandler(IAttachmentRepository attachmentRepository)
    {
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<int, Error>> Process(InitializeAttachmentCommandRequest request, CancellationToken cancellationToken)
    {
        var attachmentId = await _attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);
        return attachmentId;
    }
}
