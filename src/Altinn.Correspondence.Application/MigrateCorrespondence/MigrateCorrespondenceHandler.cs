using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Web;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class MigrateCorrespondenceHandler : IHandler<MigrateCorrespondenceRequest, MigrateCorrespondenceResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ICorrespondenceStatusRepository _statusRepository;
    private readonly ICorrespondenceNotificationRepository _notificationRepository;
    private readonly IEventBus _eventBus;
    private const bool _isMigration = true;
    private readonly InitializeCorrespondenceHelper _correspondenceHelper;
    IBackgroundJobClient _backgroundJobClient;

    public MigrateCorrespondenceHandler(IAltinnAuthorizationService altinnAuthorizationService, 
    ICorrespondenceRepository correspondenceRepository, 
    IAttachmentRepository attachmentRepository, 
    ICorrespondenceStatusRepository statusRepository, 
    IEventBus eventBus, 
    ICorrespondenceNotificationRepository notificationRepository,
    IBackgroundJobClient backgroundJobClient)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
        _statusRepository = statusRepository;
        _notificationRepository = notificationRepository;
        _correspondenceHelper = new InitializeCorrespondenceHelper(attachmentRepository, null, null);
    }

    public async Task<OneOf<MigrateCorrespondenceResponse, Error>> Process(MigrateCorrespondenceRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(request.CorrespondenceEntity.ResourceId, [ResourceAccessLevel.Send], cancellationToken, _isMigration);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        var contentError = _correspondenceHelper.ValidateCorrespondenceContent(request.CorrespondenceEntity.Content);
        if (contentError != null)
        {
            return contentError;
        }
        
        var correspondence = await _correspondenceRepository.CreateCorrespondence(request.CorrespondenceEntity, cancellationToken);

        return new MigrateCorrespondenceResponse()
        {
            Altinn2CorrespondenceId = request.Altinn2CorrespondenceId,
            CorrespondenceId = correspondence.Id,
            AttachmentMigrationStatuses = correspondence.Content?.Attachments.Select(a => new AttachmentMigrationStatus() { AttachmentId = a.AttachmentId, AttachmentStatus = AttachmentStatus.Initialized}).ToList() ?? null
        };
    }
}
