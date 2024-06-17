using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class InitializeCorrespondenceHandler : IHandler<InitializeCorrespondenceRequest, InitializeCorrespondenceResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IEventBus _eventBus;

    public InitializeCorrespondenceHandler(ICorrespondenceRepository correspondenceRepository, IAttachmentRepository attachmentRepository, IEventBus eventBus)
    {
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
        _eventBus = eventBus;
    }

    public async Task<OneOf<InitializeCorrespondenceResponse, Error>> Process(InitializeCorrespondenceRequest request, CancellationToken cancellationToken)
    {
        var attachments = request.Correspondence.Content?.Attachments;

        if (attachments != null)
        {
            foreach (var attachment in attachments)
            {
                attachment.Attachment = await ProcessAttachment(attachment, cancellationToken);
            }
        }
        var statuses = new List<CorrespondenceStatusEntity>(){
            new CorrespondenceStatusEntity
            {
                Status = GetInitializeCorrespondenceStatus(request.Correspondence),
                StatusChanged = DateTimeOffset.UtcNow
            }
        };
        request.Correspondence.Statuses = statuses;
        request.Correspondence.Notifications = ProcessNotifications(request.Correspondence.Notifications, cancellationToken);
        var correspondence = await _correspondenceRepository.InitializeCorrespondence(request.Correspondence, cancellationToken);
        await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, null, correspondence.Id.ToString(), "correspondence", null, cancellationToken);
        return new InitializeCorrespondenceResponse()
        {
            CorrespondenceId = correspondence.Id,
            AttachmentIds = correspondence.Content?.Attachments.Select(a => a.AttachmentId).ToList() ?? new List<Guid>()
        };
    }

    public CorrespondenceStatus GetInitializeCorrespondenceStatus(CorrespondenceEntity correspondence)
    {
        var status = CorrespondenceStatus.Initialized;
        if (correspondence.Content != null && correspondence.Content.Attachments.All(c => c.Attachment?.Statuses != null && c.Attachment.Statuses.All(s => s.Status == AttachmentStatus.Published)))
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
                Statuses = status,
                Created = DateTimeOffset.UtcNow
            };
        }
        return attachment;
    }

    private List<CorrespondenceNotificationEntity> ProcessNotifications(List<CorrespondenceNotificationEntity>? notifications, CancellationToken cancellationToken)
    {
        if (notifications == null) return new List<CorrespondenceNotificationEntity>();

        foreach (var notification in notifications)
        {
            notification.Statuses = new List<CorrespondenceNotificationStatusEntity>(){
                new CorrespondenceNotificationStatusEntity
                {
                     Status = "Initialized", //TODO create enums for notications?
                     StatusChanged = DateTimeOffset.UtcNow,
                     StatusText = "Initialized"
                }
              };
        }
        return notifications;
    }
}