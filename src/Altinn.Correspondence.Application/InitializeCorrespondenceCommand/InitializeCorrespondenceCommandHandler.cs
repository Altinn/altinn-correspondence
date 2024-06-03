using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeCorrespondenceCommand;

public class InitializeCorrespondenceCommandHandler : IHandler<InitializeCorrespondenceCommandRequest, InitializeCorrespondenceCommandResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    public InitializeCorrespondenceCommandHandler(ICorrespondenceRepository correspondenceRepository, IAttachmentRepository attachmentRepository)
    {
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<InitializeCorrespondenceCommandResponse, Error>> Process(InitializeCorrespondenceCommandRequest request, CancellationToken cancellationToken)
    {
        var attachments = request.Correspondence.Content.Attachments;
        foreach (var attachment in attachments)
        {
            attachment.Attachment = await ProcessAttachment(attachment, cancellationToken);
        }
        var statuses = new List<CorrespondenceStatusEntity>(){
            new CorrespondenceStatusEntity
            {
                Status = GetInitializeCorrespondenceStatus(request.Correspondence),
                StatusChanged = DateTimeOffset.UtcNow
            }
        };
        request.Correspondence.Statuses = statuses;
        var correspondence = await _correspondenceRepository.InitializeCorrespondence(request.Correspondence, cancellationToken);
        return new InitializeCorrespondenceCommandResponse()
        {
            CorrespondenceId = correspondence.Id,
            AttachmentIds = correspondence.Content.Attachments.Select(a => a.Attachment.Id).ToList()
        };
    }

    public CorrespondenceStatus GetInitializeCorrespondenceStatus(CorrespondenceEntity correspondence)
    {
        var status = CorrespondenceStatus.Initialized;
        if (correspondence.Content.Attachments.All(c => c.Attachment.Statuses != null && c.Attachment.Statuses.All(s => s.Status == AttachmentStatus.Published)))
        {
            status = CorrespondenceStatus.Published;
        }
        return status;
    }

    public async Task<AttachmentEntity> ProcessAttachment(CorrespondenceAttachmentEntity correspondenceAttachment, CancellationToken cancellationToken)
    {
        AttachmentEntity? attachment = null;
        if (correspondenceAttachment.DataLocationUrl != null)
        {
            var existingAttachment = await _attachmentRepository.GetAttachmentByUrl(correspondenceAttachment.DataLocationUrl, cancellationToken);
            if (existingAttachment != null)
            {
                attachment = existingAttachment;
            }
        }
        if (attachment == null)
        {
            var status = new List<AttachmentStatusEntity>(){
                    new AttachmentStatusEntity
                    {
                        Status = AttachmentStatus.Initialized,
                        StatusChanged = DateTimeOffset.UtcNow,
                        StatusText = AttachmentStatus.Initialized.ToString()
                    }
                };
            attachment = new AttachmentEntity
            {
                SendersReference = correspondenceAttachment.SendersReference,
                RestrictionName = correspondenceAttachment.RestrictionName,
                ExpirationTime = correspondenceAttachment.ExpirationTime,
                IntendedPresentation = correspondenceAttachment.IntendedPresentation,
                DataType = correspondenceAttachment.DataType,
                DataLocationUrl = correspondenceAttachment.DataLocationUrl,
                Statuses = status
            };
        }
        return attachment;
    }
}
