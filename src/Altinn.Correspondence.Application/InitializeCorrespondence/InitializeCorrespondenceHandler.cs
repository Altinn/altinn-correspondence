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
    private readonly IEventBus _eventBus;
    private readonly InitializeCorrespondenceHelper _initializeCorrespondenceHelper;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public InitializeCorrespondenceHandler(InitializeCorrespondenceHelper initializeCorrespondenceHelper, IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IStorageRepository storageRepository, IHostEnvironment hostEnvironment, IEventBus eventBus, IBackgroundJobClient backgroundJobClient)
    {
        _initializeCorrespondenceHelper = initializeCorrespondenceHelper;
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<OneOf<InitializeCorrespondenceResponse, Error>> Process(InitializeCorrespondenceRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(request.Correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Send }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (request.isUploadRequest && request.Attachments.Count == 0)
        {
            return Errors.NoAttachments;
        }
        var contentError = _initializeCorrespondenceHelper.ValidateCorrespondenceContent(request.Correspondence.Content);
        if (contentError != null)
        {
            return contentError;
        }
        var attachmentError = _initializeCorrespondenceHelper.ValidateAttachmentFiles(request.Attachments, request.Correspondence.Content!.Attachments, false);
        if (attachmentError != null) return attachmentError;
        var attachments = new List<AttachmentEntity>();
        if (request.Correspondence.Content!.Attachments.Count > 0)
        {
            foreach (var attachment in request.Correspondence.Content!.Attachments)
            {
                var a = await _initializeCorrespondenceHelper.ProcessAttachment(attachment, cancellationToken);
                attachments.Add(a);
            }
        }
        if (request.Attachments.Count > 0)
        {
            var uploadError = await _initializeCorrespondenceHelper.UploadAttachments(attachments, request.Attachments, cancellationToken);
            if (uploadError != null)
            {
                return uploadError;
            }
        }
        var status = _initializeCorrespondenceHelper.GetInitializeCorrespondenceStatus(request.Correspondence);
        var statuses = new List<CorrespondenceStatusEntity>(){
            new CorrespondenceStatusEntity
            {
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = status.ToString()
            }
        };
        if (attachments != null)
        {
            request.Correspondence.Content!.Attachments = attachments.Select(a => new CorrespondenceAttachmentEntity
            {
                Attachment = a,
                Created = DateTimeOffset.UtcNow,

            }).ToList();
        }

        request.Correspondence.Statuses = statuses;
        request.Correspondence.Notifications = _initializeCorrespondenceHelper.ProcessNotifications(request.Correspondence.Notifications, cancellationToken);
        var correspondence = await _correspondenceRepository.CreateCorrespondence(request.Correspondence, cancellationToken);
        _backgroundJobClient.Schedule<PublishCorrespondenceService>((service) => service.Publish(correspondence.Id, cancellationToken), correspondence.VisibleFrom);
        await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);

        return new InitializeCorrespondenceResponse()
        {
            CorrespondenceId = correspondence.Id,
            AttachmentIds = correspondence.Content?.Attachments.Select(a => a.AttachmentId).ToList() ?? new List<Guid>()
        };
    }
}
