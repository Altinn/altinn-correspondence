using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Persistence.Helpers;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.ExpireAttachment;

public class ExpireAttachmentHandler(
    ILogger<ExpireAttachmentHandler> logger,
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IStorageRepository storageRepository,
    IAltinnRegisterService altinnRegisterService,
    IBackgroundJobClient backgroundJobClient) : IHandler<Guid, Task>
{
    public async Task<OneOf<Task, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Expiring attachment {AttachmentId}", attachmentId);

        var attachment = await attachmentRepository.GetAttachmentById(attachmentId, includeStatus: true, cancellationToken);
        if (attachment is null)
        {
            logger.LogError("Attachment {AttachmentId} not found when expiring attachment", attachmentId);
            throw new InvalidOperationException($"Attachment {attachmentId} not found");
        }

        if (attachment.StatusHasBeen(AttachmentStatus.Purged) || attachment.StatusHasBeen(AttachmentStatus.Expired))
        {
            logger.LogInformation("Attachment {AttachmentId} already purged or expired; skipping expiration", attachmentId);
            return Task.CompletedTask;
        }

        var now = DateTimeOffset.UtcNow;
        var maxExpirationTime = await attachmentRepository.GetMaxExpirationTimeForAttachment(attachmentId, cancellationToken);
        if (maxExpirationTime is null)
        {
            logger.LogWarning("Attachment {AttachmentId} has correspondenceAttachment with no max expiration time; skipping expiration", attachmentId);
            return Task.CompletedTask;
        }

        if (maxExpirationTime > now)
        {
            logger.LogInformation("Attachment {AttachmentId} has a correspondenceAttachment set to expire at {ExpirationTime}; skipping this expiration job", attachmentId, maxExpirationTime);
            return Task.CompletedTask;
        }

        var party = await altinnRegisterService.LookUpPartyById(attachment.Sender, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for sender {Sender} when expiring attachment", attachment.Sender);
            throw new InvalidOperationException($"Could not find party UUID for sender {attachment.Sender}");
        }

        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            var expireIdempotencyId = attachmentId.CreateVersion5("ExpireAttachment");
            try
            {
                await idempotencyKeyRepository.CreateAsync(new IdempotencyKeyEntity
                {
                    Id = expireIdempotencyId,
                    CorrespondenceId = null,
                    AttachmentId = attachmentId,
                    PartyUrn = attachment.Sender,
                    StatusAction = null,
                    IdempotencyType = IdempotencyType.ExpireAttachment
                }, cancellationToken);
            }
            catch (DbUpdateException e) when (e.IsPostgresUniqueViolation())
            {
                logger.LogInformation("Expire already processed for attachment {AttachmentId}; skipping", attachmentId);
                return Task.CompletedTask;
            }

            await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                Status = AttachmentStatus.Expired,
                StatusText = "The attachment has expired",
                StatusChanged = DateTimeOffset.UtcNow,
                PartyUuid = partyUuid
            }, cancellationToken);

            await storageRepository.PurgeAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);

            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(
                AltinnEventType.AttachmentExpired,
                attachment.ResourceId,
                attachment.Id.ToString(),
                "Expiration",
                attachment.Sender,
                CancellationToken.None));

            logger.LogInformation("Successfully expired attachment {AttachmentId} with filename {FileName}", attachmentId, attachment.FileName);
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}