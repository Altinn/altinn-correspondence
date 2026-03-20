using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.ProcessLegacyParty;
using Altinn.Correspondence.Application.SendNotificationOrder;
using Altinn.Correspondence.Application.SendSlackNotification;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Persistence.Helpers;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.PublishCorrespondence;

public class PublishCorrespondenceHandler(
    IAltinnRegisterService altinnRegisterService,
    ILogger<PublishCorrespondenceHandler> logger,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IContactReservationRegistryService contactReservationRegistryService,
    IBackgroundJobClient backgroundJobClient,
    IIdempotencyKeyRepository idempotencyKeyRepository) : IHandler<Guid, Task>
{
    public async Task<OneOf<Task, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting publish process with lock for correspondence {CorrespondenceId}", correspondenceId);
        var operationTimestamp = DateTimeOffset.UtcNow;        
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        var senderParty = await altinnRegisterService.LookUpPartyById(correspondence!.Sender, cancellationToken);
        var recipientParty = await altinnRegisterService.LookUpPartyById(correspondence!.Recipient, cancellationToken);
        var senderPartyUuid = senderParty?.PartyUuid;
        var recipientPartyUuid = recipientParty?.PartyUuid;
        bool hasDialogportenDialog = correspondence!.ExternalReferences.Any(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId);
        logger.LogInformation("Correspondence {CorrespondenceId} has Dialogporten dialog: {HasDialog}", correspondenceId, hasDialogportenDialog);

        if (correspondence.StatusHasBeen(CorrespondenceStatus.Published) || correspondence.StatusHasBeen(CorrespondenceStatus.Failed))
        {
            logger.LogInformation("Skipping publish for correspondence {CorrespondenceId} - already published or failed", correspondenceId);
            return Task.CompletedTask;
        }

        if (!await correspondenceRepository.AreAllAttachmentsPublished(correspondence.Id, cancellationToken))
        {
            logger.LogInformation("Skipping this publish job for correspondence {CorrespondenceId} - not all attachments are published yet - the final attachment publish will enqueue this job again", correspondenceId);
            return Task.CompletedTask;
        }

        var errorMessage = "";
        if (correspondence == null)
        {
            errorMessage = "Correspondence " + correspondenceId + " not found when publishing";
        }
        else if (senderPartyUuid is not Guid)
        {
            errorMessage = $"Party for sender {correspondence.Sender} not found in Altinn Register when publishing";
        }
        else if (recipientPartyUuid is not Guid)
        {
            errorMessage = $"Party for recipient {correspondence.Recipient} not found in Altinn Register when publishing";
        }
        else if (!await IsCorrespondenceReadyForPublish(correspondence, senderPartyUuid.Value, operationTimestamp, cancellationToken))
        {
            errorMessage = $"Correspondence {correspondenceId} not ready for publish";
        }
        else if (!hasDialogportenDialog)
        {
            errorMessage = $"Dialogporten dialog not created for correspondence {correspondenceId}";
        }
        else if (await HasRecipientBeenSetToReservedInKRR(correspondence, cancellationToken))
        {
            errorMessage = $"Recipient of {correspondenceId} has been set to reserved in kontakt- og reserverasjonsregisteret ('KRR')";
        }
        else if (
            correspondence.IsConfidential &&
            !string.IsNullOrEmpty(recipientParty!.OrgNumber) &&
            !await altinnRegisterService.HasPartyRequiredRoles(correspondence.Recipient, recipientPartyUuid.Value, correspondence.IsConfidential, cancellationToken))
        {
            // Only check for confidential pending #1444. Remove IsConfidential condition after Register has been updated.
            errorMessage = $"Recipient of {correspondenceId} lacks roles required to read correspondence. Consider sending physical mail to this recipient instead.";
        }
        CorrespondenceStatusEntity status;
        AltinnEventType eventType = AltinnEventType.CorrespondencePublished;

        return await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            var publishIdempotencyId = correspondenceId.CreateVersion5("PublishCorrespondence");
            try
            {
                await idempotencyKeyRepository.CreateAsync(new IdempotencyKeyEntity
                {
                    Id = publishIdempotencyId,
                    CorrespondenceId = correspondenceId,
                    AttachmentId = null,
                    PartyUrn = null,
                    StatusAction = null,
                    IdempotencyType = IdempotencyType.PublishCorrespondence
                }, cancellationToken);
            }
            catch (DbUpdateException e) when (e.IsPostgresUniqueViolation())
            {
                logger.LogInformation("Publish already completed for correspondence {CorrespondenceId}; skipping", correspondenceId);
                return Task.CompletedTask;
            }

            if (errorMessage.Length > 0)
            {
                logger.LogError("Publish failed for correspondence {CorrespondenceId}: {ErrorMessage}", correspondenceId, errorMessage);
                status = new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Failed,
                    StatusChanged = operationTimestamp,
                    StatusText = errorMessage,
                    PartyUuid = senderPartyUuid ?? Guid.Empty
                };
                backgroundJobClient.Enqueue<SendSlackNotificationHandler>(
                    handler => handler.Process("Correspondence failed", errorMessage));
                eventType = AltinnEventType.CorrespondencePublishFailed;
                if (hasDialogportenDialog)
                {
                    logger.LogInformation("Purging Dialogporten dialog for failed correspondence {CorrespondenceId}", correspondenceId);
                    backgroundJobClient.Enqueue<IDialogportenService>(dialogportenService => dialogportenService.PurgeCorrespondenceDialog(correspondenceId));
                }
            }
            else
            {
                logger.LogInformation("Publishing correspondence {CorrespondenceId}", correspondenceId);
                status = new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Published,
                    StatusChanged = operationTimestamp,
                    StatusText = CorrespondenceStatus.Published.ToString(),
                    PartyUuid = senderPartyUuid ?? Guid.Empty
                };
                await correspondenceRepository.UpdatePublished(correspondenceId, status.StatusChanged, cancellationToken);
                backgroundJobClient.Enqueue<ProcessLegacyPartyHandler>((handler) => handler.Process(correspondence!.Recipient, null, cancellationToken));
                backgroundJobClient.Enqueue<SendNotificationOrderHandler>((handler) => handler.Process(correspondence!.Id, cancellationToken));
            }

            await correspondenceStatusRepository.AddCorrespondenceStatus(status, cancellationToken);
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(eventType, correspondence!.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
            if (status.Status == CorrespondenceStatus.Published)
            {
                backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(eventType, correspondence!.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, CancellationToken.None));
            }
            logger.LogInformation("Successfully completed publish process for correspondence {CorrespondenceId} with status {Status}", correspondenceId, status.Status);
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }

    private async Task<bool> IsCorrespondenceReadyForPublish(CorrespondenceEntity correspondence, Guid partyUuid, DateTimeOffset operationTimestamp, CancellationToken cancellationToken)
    {
        if (correspondence.GetHighestStatus()?.Status != CorrespondenceStatus.ReadyForPublish)
        {
            if (await correspondenceRepository.AreAllAttachmentsPublished(correspondence.Id, cancellationToken))
            {
                await correspondenceStatusRepository.AddCorrespondenceStatus(
                    new CorrespondenceStatusEntity
                    {
                        CorrespondenceId = correspondence.Id,
                        Status = CorrespondenceStatus.ReadyForPublish,
                        StatusChanged = operationTimestamp.AddMilliseconds(-1),
                        StatusText = CorrespondenceStatus.ReadyForPublish.ToString(),
                        PartyUuid = partyUuid
                    },
                    cancellationToken
                );
            }
            else 
            {
                return false;
            }
        }
        return true;
    }

    private async Task<bool> HasRecipientBeenSetToReservedInKRR(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        if (correspondence.IgnoreReservation != true && correspondence.GetRecipientUrn().IsSocialSecurityNumber())
        {
            var isReserved = await contactReservationRegistryService.IsPersonReserved(correspondence.GetRecipientUrn().WithoutPrefix());
            if (isReserved)
            {
                return true;
            }
        }
        return false;
    }
}