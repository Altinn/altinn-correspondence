using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeAttachmentCommand;

public class InitializeAttachmentCommandHandler : IHandler<InitializeAttachmentCommandRequest, Guid>
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IEventBus _eventBus;
    public InitializeAttachmentCommandHandler(IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IEventBus eventBus)
    {
        _attachmentRepository = attachmentRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
        _eventBus = eventBus;
    }

    public async Task<OneOf<Guid, Error>> Process(InitializeAttachmentCommandRequest request, CancellationToken cancellationToken)
    {
        var attachmentId = await _attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);
        var status = new AttachmentStatusEntity
        {
            AttachmentId = attachmentId,
            StatusChanged = DateTimeOffset.UtcNow,
            Status = AttachmentStatus.Initialized,
            StatusText = AttachmentStatus.Initialized.ToString()
        };
        await _attachmentStatusRepository.AddAttachmentStatus(status, cancellationToken);
        await _eventBus.Publish(AltinnEventType.AttachmentInitialized, null, attachmentId.ToString(), "attachment", null, cancellationToken);
        return attachmentId;
    }
}
