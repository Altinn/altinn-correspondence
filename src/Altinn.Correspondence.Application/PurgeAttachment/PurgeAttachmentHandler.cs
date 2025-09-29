using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Altinn.Correspondence.Common.Helpers;
using Hangfire;

namespace Altinn.Correspondence.Application.PurgeAttachment;

public class PurgeAttachmentHandler(
    IAltinnRegisterService altinnRegisterService,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    IStorageRepository storageRepository,
    ICorrespondenceRepository correspondenceRepository,
    IBackgroundJobClient backgroundJobClient,
    ILogger<PurgeAttachmentHandler> logger) : IHandler<Guid, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing purge request for attachment {AttachmentId}", attachmentId);
        var attachment = await attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            logger.LogError("Attachment with id {AttachmentId} not found", attachmentId);
            return AttachmentErrors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            attachment.ResourceId,
            attachment.Sender.WithoutPrefix(),
            attachment.Id.ToString(),
            cancellationToken);
        if (!hasAccess)
        {
            logger.LogWarning("Access denied for attachment {AttachmentId} - user does not have sender access", attachmentId);
            return AuthorizationErrors.NoAccessToResource;
        }
        logger.LogInformation("User has sender access to attachment {AttachmentId}", attachmentId);
        if (attachment.StatusHasBeen(AttachmentStatus.Purged))
        {
            logger.LogWarning("Attachment {AttachmentId} has already been purged at {PurgedTime}", 
                attachmentId,
                attachment.Statuses.FirstOrDefault(s => s.Status == AttachmentStatus.Purged)?.StatusChanged);
            return AttachmentErrors.FileHasBeenPurged;
        }
        logger.LogInformation("Checking for existing correspondences for attachment {AttachmentId}", attachmentId);
        var correspondences = await correspondenceRepository.GetCorrespondencesByAttachmentId(attachmentId, false);
        if (correspondences.Count != 0)
        {
            logger.LogWarning("Cannot purge attachment {AttachmentId} - it is linked to {Count} correspondences: {CorrespondenceIds}", 
                attachmentId, 
                correspondences.Count,
                string.Join(", ", correspondences.Select(c => c.Id)));
            return AttachmentErrors.PurgeAttachmentWithExistingCorrespondence;
        }
        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for organization {OrganizationId}", user.GetCallerOrganizationId());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        logger.LogInformation("Retrieved party UUID {PartyUuid} for organization {OrganizationId}", partyUuid, user.GetCallerOrganizationId());
        logger.LogInformation("Starting purge process for attachment {AttachmentId} with storage provider {StorageProvider}", 
            attachmentId,
            attachment.StorageProvider);
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            try
            {
                await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
                {
                    AttachmentId = attachmentId,
                    Status = AttachmentStatus.Purged,
                    StatusText = "Attachment has been purged",
                    StatusChanged = DateTimeOffset.UtcNow,
                    PartyUuid = partyUuid
                }, cancellationToken);
                await storageRepository.PurgeAttachment(attachmentId, attachment.StorageProvider, cancellationToken);
                logger.LogInformation("Successfully purged attachment {AttachmentId} with filename {FileName}", 
                    attachmentId,
                    attachment.FileName);
                return attachmentId;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error purging attachment {AttachmentId} from storage provider {StorageProvider}", 
                    attachmentId,
                    attachment.StorageProvider);
                throw;
            }
        }, logger, cancellationToken);
    }
}
