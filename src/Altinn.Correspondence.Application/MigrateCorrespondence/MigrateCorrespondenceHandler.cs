using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Web;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
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
    private readonly InitializeCorrespondenceHelper _initializeCorrespondenceHelper;
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
        _initializeCorrespondenceHelper = initializeCorrespondenceHelper;
    }

    public async Task<OneOf<MigrateCorrespondenceResponse, Error>> Process(MigrateCorrespondenceRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckMigrationAccess(request.CorrespondenceEntity.ResourceId, [ResourceAccessLevel.Send], cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        var contentError = _initializeCorrespondenceHelper.ValidateCorrespondenceContent(request.CorrespondenceEntity.Content);
        if (contentError != null)
        {
            return contentError;
        }

        // Validate that existing attachments are correct
        var existingAttachments = await _initializeCorrespondenceHelper.GetExistingAttachments(request.ExistingAttachments);
        if (existingAttachments.Count != request.ExistingAttachments.Count)
        {
            return Errors.ExistingAttachmentNotFound;
        }
        // Validate that existing attachments are published
        var anyExistingAttachmentsNotPublished = existingAttachments.Any(a => a.GetLatestStatus()?.Status != AttachmentStatus.Published);
        if (anyExistingAttachmentsNotPublished)
        {
            return Errors.AttachmentNotPublished;
        }

        request.CorrespondenceEntity.Content.Attachments.AddRange
        (
            existingAttachments.Select(a => new CorrespondenceAttachmentEntity() 
            { 
                Attachment = a, 
                Created = DateTimeOffset.Now
            })
        );
        
        var correspondence = await _correspondenceRepository.CreateCorrespondence(request.CorrespondenceEntity, cancellationToken);

        return new MigrateCorrespondenceResponse()
        {
            Altinn2CorrespondenceId = request.Altinn2CorrespondenceId,
            CorrespondenceId = correspondence.Id,
            AttachmentMigrationStatuses = correspondence.Content?.Attachments.Select(a => new AttachmentMigrationStatus() { AttachmentId = a.AttachmentId, AttachmentStatus = AttachmentStatus.Initialized}).ToList() ?? null
        };
    }
}
