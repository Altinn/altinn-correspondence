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
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IEventBus _eventBus;
    private readonly IStorageRepository _storageRepository;
    private readonly IHostEnvironment _hostEnvironment;
    IBackgroundJobClient _backgroundJobClient;

    public InitializeCorrespondenceHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IStorageRepository storageRepository, IHostEnvironment hostEnvironment, IEventBus eventBus, IBackgroundJobClient backgroundJobClient)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
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
        if (request.isUploadRequest && request.Attachments.Count == 0)
        {
            return Errors.NoAttachments;
        }
        InitializeCorrespondenceHelper initializeCorrespondenceHelper = new InitializeCorrespondenceHelper(_correspondenceRepository, _correspondenceStatusRepository, _attachmentStatusRepository, _attachmentRepository, _storageRepository, _hostEnvironment);
        var contentError = initializeCorrespondenceHelper.ValidateCorrespondenceContent(request.Correspondence.Content);
        if (contentError != null)
        {
            return contentError;
        }
        var attachmentError = initializeCorrespondenceHelper.ValidateAttachmentFiles(request.Attachments, request.Correspondence.Content!.Attachments, false);
        if (attachmentError != null)
        {
            return attachmentError;
        }
        var attachments = request.Correspondence.Content!.Attachments;
        if (attachments != null)
        {
            foreach (var attachment in attachments)
            {
                attachment.Attachment = await initializeCorrespondenceHelper.ProcessAttachment(attachment, request.Correspondence, cancellationToken);
            }
        }
        var status = initializeCorrespondenceHelper.GetInitializeCorrespondenceStatus(request.Correspondence);
        var statuses = new List<CorrespondenceStatusEntity>(){
            new CorrespondenceStatusEntity
            {
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = status.ToString()
            }
        };
        request.Correspondence.Statuses = statuses;
        request.Correspondence.Notifications = initializeCorrespondenceHelper.ProcessNotifications(request.Correspondence.Notifications, cancellationToken);
        var correspondence = await _correspondenceRepository.CreateCorrespondence(request.Correspondence, cancellationToken);
        _backgroundJobClient.Schedule<PublishCorrespondenceService>((service) => service.Publish(correspondence.Id, cancellationToken), correspondence.VisibleFrom);
        await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        if (request.Attachments.Count > 0)
        {
            var uploadError = await initializeCorrespondenceHelper.UploadAttachments(request.Correspondence, request.Attachments, cancellationToken);
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
}
