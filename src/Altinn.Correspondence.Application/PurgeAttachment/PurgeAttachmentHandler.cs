using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Altinn.Correspondence.Common.Helpers;
using Hangfire;
using System.Linq;

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
        logger.LogDebug("Retrieved attachment {AttachmentId} with filename {FileName} and status {Status}", 
            attachmentId, 
            attachment.FileName,
            attachment.Statuses.OrderByDescending(s => s.StatusChanged).FirstOrDefault()?.Status);
        logger.LogDebug("Checking sender access for attachment {AttachmentId} and resource {ResourceId}", 
            attachmentId, 
            attachment.ResourceId);
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
        logger.LogDebug("User has sender access to attachment {AttachmentId}", attachmentId);
        if (attachment.StatusHasBeen(AttachmentStatus.Purged))
        {
            logger.LogWarning("Attachment {AttachmentId} has already been purged at {PurgedTime}", 
                attachmentId,
                attachment.Statuses.FirstOrDefault(s => s.Status == AttachmentStatus.Purged)?.StatusChanged);
            return AttachmentErrors.FileHasBeenPurged;
        }
        logger.LogDebug("Checking for existing correspondences for attachment {AttachmentId}", attachmentId);
        var correspondences = await correspondenceRepository.GetCorrespondencesByAttachmentId(attachmentId, false);
        if (correspondences.Count != 0)
        {
            logger.LogWarning("Cannot purge attachment {AttachmentId} - it is linked to {Count} correspondences: {CorrespondenceIds}", 
                attachmentId, 
                correspondences.Count,
                string.Join(", ", correspondences.Select(c => c.Id)));
            return AttachmentErrors.PurgeAttachmentWithExistingCorrespondence;
        }
        logger.LogDebug("Looking up party for organization {OrganizationId}", user.GetCallerOrganizationId());
        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for organization {OrganizationId}", user.GetCallerOrganizationId());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        logger.LogDebug("Retrieved party UUID {PartyUuid} for organization {OrganizationId}", partyUuid, user.GetCallerOrganizationId());
        logger.LogInformation("Starting purge process for attachment {AttachmentId} with storage provider {StorageProvider}", 
            attachmentId,
            attachment.StorageProvider);
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            try
            {
                logger.LogDebug("Adding Purged status for attachment {AttachmentId} by party {PartyUuid}", 
                    attachmentId,
                    partyUuid);
                await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
                {
                    AttachmentId = attachmentId,
                    Status = AttachmentStatus.Purged,
                    StatusText = "Attachment has been purged",
                    StatusChanged = DateTimeOffset.UtcNow,
                    PartyUuid = partyUuid
                }, cancellationToken);
                logger.LogDebug("Purging attachment {AttachmentId} from storage provider {StorageProvider}", 
                    attachmentId, 
                    attachment.StorageProvider);
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
