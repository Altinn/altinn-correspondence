using System.Security.Claims;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class MigrateCorrespondenceHandler(
    InitializeCorrespondenceHelper initializeCorrespondenceHelper,
    IAltinnAuthorizationService altinnAuthorizationService,
    ICorrespondenceRepository correspondenceRepository,
    ILogger<MigrateCorrespondenceHandler> logger) : IHandler<MigrateCorrespondenceRequest, MigrateCorrespondenceResponse>
{
    public async Task<OneOf<MigrateCorrespondenceResponse, Error>> Process(MigrateCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var hasAccess = await altinnAuthorizationService.CheckMigrationAccess(request.CorrespondenceEntity.ResourceId, [ResourceAccessLevel.Write], cancellationToken);
        if (!hasAccess)
        {
            return AuthorizationErrors.NoAccessToResource;
        }

        var contentError = initializeCorrespondenceHelper.ValidateCorrespondenceContent(request.CorrespondenceEntity.Content);
        if (contentError != null)
        {
            return contentError;
        }

        // Validate that existing attachments are correct
        var getExistingAttachments = await initializeCorrespondenceHelper.GetExistingAttachments(request.ExistingAttachments, request.CorrespondenceEntity.Sender);
        if (getExistingAttachments.IsT1) return getExistingAttachments.AsT1;
        var existingAttachments = getExistingAttachments.AsT0;
        if (existingAttachments.Count != request.ExistingAttachments.Count)
        {
            return CorrespondenceErrors.ExistingAttachmentNotFound;
        }
        // Validate that existing attachments are published
        var anyExistingAttachmentsNotPublished = existingAttachments.Any(a => a.GetLatestStatus()?.Status != AttachmentStatus.Published);
        if (anyExistingAttachmentsNotPublished)
        {
            return CorrespondenceErrors.MissingAttachments;
        }

        return await TransactionWithRetriesPolicy.Execute<MigrateCorrespondenceResponse>(async (cancellationToken) =>
        {
            request.CorrespondenceEntity.Content.Attachments.AddRange
            (
                existingAttachments.Select(a => new CorrespondenceAttachmentEntity()
                {
                    Attachment = a,
                    Created = DateTimeOffset.Now
                })
            );

            var correspondence = await correspondenceRepository.CreateCorrespondence(request.CorrespondenceEntity, cancellationToken);
            return new MigrateCorrespondenceResponse()
            {
                Altinn2CorrespondenceId = request.Altinn2CorrespondenceId,
                CorrespondenceId = correspondence.Id,
                AttachmentMigrationStatuses = correspondence.Content?.Attachments.Select(a => new AttachmentMigrationStatus() { AttachmentId = a.AttachmentId, AttachmentStatus = AttachmentStatus.Initialized }).ToList() ?? null
            };
        }, logger, cancellationToken);

    }
}
