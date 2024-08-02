using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class InitializeCorrespondenceHandler : IHandler<InitializeCorrespondenceRequest, InitializeCorrespondenceResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IEventBus _eventBus;
    private readonly IStorageRepository _storageRepository;
    private readonly IHostEnvironment _hostEnvironment;
    IBackgroundJobClient _backgroundJobClient;

    public InitializeCorrespondenceHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository, IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IStorageRepository storageRepository, IHostEnvironment hostEnvironment, IEventBus eventBus, IBackgroundJobClient backgroundJobClient)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
        _storageRepository = storageRepository;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<OneOf<InitializeCorrespondenceResponse, Error>> Process(InitializeCorrespondenceRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(request.Correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Send }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (!TextValidation.ValidatePlainText(request.Correspondence.Content?.MessageTitle))
        {
            return Errors.MessageTitleIsNotPlainText;
        }
        if (!TextValidation.ValidateMarkdown(request.Correspondence.Content?.MessageBody))
        {
            return Errors.MessageBodyIsNotMarkdown;
        }
        if (!TextValidation.ValidateMarkdown(request.Correspondence.Content?.MessageSummary))
        {
            return Errors.MessageSummaryIsNotMarkdown;
        }
        var attachmentError = ValidateAttachmentFiles(request.Attachments, request.Correspondence.Content?.Attachments);
        if (attachmentError != null)
        {
            return attachmentError;
        }
        var attachments = request.Correspondence.Content?.Attachments;
        if (attachments != null)
        {
            foreach (var attachment in attachments)
            {
                attachment.Attachment = await ProcessAttachment(attachment, request.Correspondence, cancellationToken);
            }
        }

        var status = GetInitializeCorrespondenceStatus(request.Correspondence);
        var statuses = new List<CorrespondenceStatusEntity>(){
            new CorrespondenceStatusEntity
            {
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = status.ToString()
            }
        };
        request.Correspondence.Statuses = statuses;
        request.Correspondence.Notifications = ProcessNotifications(request.Correspondence.Notifications, cancellationToken);
        var correspondence = await _correspondenceRepository.InitializeCorrespondence(request.Correspondence, cancellationToken);
        _backgroundJobClient.Schedule<PublishCorrespondenceService>((service) => service.Publish(correspondence.Id, cancellationToken), request.Correspondence.VisibleFrom);
        await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, request.Correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", request.Correspondence.Sender, cancellationToken);

        if (request.Attachments.Count > 0)
        {
            var uploadError = await UploadAttachments(request, cancellationToken);
            if (uploadError != null)
            {
                return uploadError;
            }
        }

        return new InitializeCorrespondenceResponse()
        {
            CorrespondenceId = correspondence.Id,
            AttachmentIds = correspondence.Content?.Attachments.Select(a => a.AttachmentId).ToList() ?? new List<Guid>()
        };
    }
    public async Task<Error?> UploadAttachments(InitializeCorrespondenceRequest request, CancellationToken cancellationToken)
    {
        foreach (var file in request.Attachments)
        {
            var attachment = request.Correspondence.Content?.Attachments.FirstOrDefault(a => a.Name == file.FileName);
            if (attachment == null || attachment.Attachment == null)
            {
                return Errors.UploadedFilesDoesNotMatchAttachments;
            }
            UploadHelper uploadHelper = new UploadHelper(_attachmentStatusRepository, _attachmentRepository, _storageRepository, _hostEnvironment);
            var uploadResponse = await uploadHelper.UploadAttachment(file.OpenReadStream(), attachment.AttachmentId, cancellationToken);
            var error = uploadResponse.Match(
                _ => { return null; },
                error => { return error; }
            );
            if (error != null) return error;
        }
        return null;
    }



    public CorrespondenceStatus GetInitializeCorrespondenceStatus(CorrespondenceEntity correspondence)
    {
        var status = CorrespondenceStatus.Initialized;
        if (correspondence.Content != null && correspondence.Content.Attachments.All(c => c.Attachment?.Statuses != null && c.Attachment.Statuses.All(s => s.Status == AttachmentStatus.Published)))
        {
            status = correspondence.VisibleFrom < DateTime.UtcNow ? CorrespondenceStatus.Published : CorrespondenceStatus.ReadyForPublish;
        }
        return status;
    }

    public async Task<AttachmentEntity> ProcessAttachment(CorrespondenceAttachmentEntity correspondenceAttachment, CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        AttachmentEntity? attachment = null;
        if (!String.IsNullOrEmpty(correspondenceAttachment.DataLocationUrl))
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
                ResourceId = correspondence.ResourceId,
                Sender = correspondence.Sender,
                SendersReference = correspondenceAttachment.SendersReference,
                RestrictionName = correspondenceAttachment.RestrictionName,
                ExpirationTime = correspondenceAttachment.ExpirationTime,
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

    public Error? ValidateAttachmentFiles(List<IFormFile> files, List<CorrespondenceAttachmentEntity> attachments)
    {
        if (files.Count > 0)
        {
            var maxUploadSize = long.Parse(int.MaxValue.ToString());
            foreach (var attachment in attachments)
            {
                var file = files.FirstOrDefault(a => a.FileName == attachment.Name);
                if (file == null) return Errors.UploadedFilesDoesNotMatchAttachments;
                if (file.Length > maxUploadSize || file.Length == 0) return Errors.InvalidFileSize;
            }
        }
        return null;
    }
}
