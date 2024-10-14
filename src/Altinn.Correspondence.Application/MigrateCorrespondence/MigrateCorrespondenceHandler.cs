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
    private readonly InitializeCorrespondenceHelper _correspondenceHelper;
    IBackgroundJobClient _backgroundJobClient;

    public MigrateCorrespondenceHandler(
        InitializeCorrespondenceHelper initializeCorrespondenceHelper,
        IAltinnAuthorizationService altinnAuthorizationService, 
        ICorrespondenceRepository correspondenceRepository, 
        IBackgroundJobClient backgroundJobClient)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _backgroundJobClient = backgroundJobClient;
        _correspondenceHelper = initializeCorrespondenceHelper;
    }

    public async Task<OneOf<MigrateCorrespondenceResponse, Error>> Process(MigrateCorrespondenceRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckMigrationAccess(request.CorrespondenceEntity.ResourceId, [ResourceAccessLevel.Write], cancellationToken);
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
