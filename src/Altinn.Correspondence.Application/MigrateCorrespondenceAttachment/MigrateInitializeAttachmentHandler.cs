using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class MigrateInitializeAttachmentHandler : IHandler<InitializeAttachmentRequest, Guid>
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IEventBus _eventBus;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;

    public MigrateInitializeAttachmentHandler(IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IEventBus eventBus, IAltinnAuthorizationService altinnAuthorizationService)
    {
        _attachmentRepository = attachmentRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
        _eventBus = eventBus;
        _altinnAuthorizationService = altinnAuthorizationService;
    }

    public async Task<OneOf<Guid, Error>> Process(InitializeAttachmentRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckMigrationAccess(request.Attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Send }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        var attachment = await _attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);
        var status = new AttachmentStatusEntity
        {
            AttachmentId = attachment.Id,
            StatusChanged = DateTimeOffset.UtcNow,
            Status = AttachmentStatus.Initialized,
            StatusText = AttachmentStatus.Initialized.ToString()
        };
        await _attachmentStatusRepository.AddAttachmentStatus(status, cancellationToken);
        return attachment.Id;
    }
}
