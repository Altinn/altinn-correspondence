using System.Security.Claims;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.ExpireAttachment;

public class ExpireAttachmentHandler(
    ILogger<ExpireAttachmentHandler> logger,
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    IStorageRepository storageRepository,
    IAltinnRegisterService altinnRegisterService,
    IBackgroundJobClient backgroundJobClient) : IHandler<Guid, Task>
{
    public async Task<OneOf<Task, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Expiring attachment {AttachmentId}", attachmentId);
        var attachment = await attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment is null)
        {
            logger.LogError("Attachment {AttachmentId} not found when expiring attachment", attachmentId);
            throw new InvalidOperationException($"Attachment {attachmentId} not found");
        }

        if (attachment.StatusHasBeen(AttachmentStatus.Purged))
        {
            logger.LogInformation("Attachment {AttachmentId} already purged; skipping expiration", attachmentId);
            return Task.CompletedTask;
        }

        if (attachment.ExpirationTime is null)
        {
            logger.LogError("Attachment {AttachmentId} is missing expiration time when expiring attachment", attachmentId);
            throw new InvalidOperationException($"Attachment {attachmentId} has no expiration time");
        }

        if (attachment.ExpirationTime > DateTimeOffset.UtcNow)
        {
            logger.LogError("The attachment {AttachmentId} was not set to expire at the scheduled expiration job time", attachmentId);
            throw new InvalidOperationException($"Attachment {attachmentId} is not set to expire at this time");
        }

        var party = await altinnRegisterService.LookUpPartyById(attachment.Sender, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for sender {Sender} when expiring attachment", attachment.Sender);
            throw new InvalidOperationException($"Could not find party UUID for sender {attachment.Sender}");
        }

        return await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                Status = AttachmentStatus.Purged,
                StatusText = "The attachment has expired",
                StatusChanged = DateTimeOffset.UtcNow,
                PartyUuid = partyUuid
            }, cancellationToken);

            await storageRepository.PurgeAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);

            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(
                AltinnEventType.AttachmentPurged,
                attachment.ResourceId,
                attachment.Id.ToString(),
                "Expiration",
                attachment.Sender,
                CancellationToken.None));

            logger.LogInformation("Successfully expired attachment {AttachmentId}", attachmentId);
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}