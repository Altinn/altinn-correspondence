using Altinn.Correspondence.Application.CancelNotification;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.ProcessLegacyParty;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Helpers;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using Slack.Webhooks;
using System.Security.Claims;
using Altinn.Correspondence.Integrations.Redlock;
using Altinn.Correspondence.Application.Settings;

namespace Altinn.Correspondence.Application.PublishCorrespondence;

public class PublishCorrespondenceHandler(
    IAltinnRegisterService altinnRegisterService,
    ILogger<PublishCorrespondenceHandler> logger,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IContactReservationRegistryService contactReservationRegistryService,
    IHostEnvironment hostEnvironment,
    ISlackClient slackClient,
    SlackSettings slackSettings,
    IBackgroundJobClient backgroundJobClient,
    IDistributedLockHelper distributedLockHelper) : IHandler<Guid, Task>
{

    public async Task<OneOf<Task, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting publish process for correspondence {CorrespondenceId}", correspondenceId);
        var lockKey = $"publish-correspondence-{correspondenceId}";

        OneOf<Task, Error>? innerResult = null;
        var (wasSkipped, lockAcquired) = await distributedLockHelper.ExecuteWithConditionalLockAsync(
            lockKey, 
            async (cancellationToken) => await ShouldSkipCheck(correspondenceId, cancellationToken),
            async (cancellationToken) => innerResult = await ProcessWithLock(correspondenceId, user, cancellationToken),
            DistributedLockHelper.DefaultRetryCount,
            DistributedLockHelper.DefaultRetryDelayMs,
            DistributedLockHelper.DefaultLockExpirySeconds,
            cancellationToken);

        if (wasSkipped)
        {
            logger.LogWarning("Skipping publish process for correspondence {CorrespondenceId} - already published or failed", correspondenceId);
            return Task.CompletedTask;
        }
        if (!lockAcquired)
        {
            logger.LogWarning("Failed to acquire lock for correspondence {CorrespondenceId} - another process may be handling this correspondence", correspondenceId);
            return Task.CompletedTask;
        }
        return innerResult ?? Task.CompletedTask;
    }

    public async Task<bool> ShouldSkipCheck(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        var status = correspondence?.GetHighestStatus()?.Status;
        var shouldSkip = status == CorrespondenceStatus.Published || status == CorrespondenceStatus.Failed;
        if (shouldSkip)
        {
            logger.LogInformation("Skipping publish for correspondence {CorrespondenceId} - current status: {Status}", correspondenceId, status);
        }
        return shouldSkip;
    }

    public async Task<OneOf<Task, Error>> ProcessWithLock(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting publish process with lock for correspondence {CorrespondenceId}", correspondenceId);
        var operationTimestamp = DateTimeOffset.UtcNow;        
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        var senderParty = await altinnRegisterService.LookUpPartyById(correspondence!.Sender, cancellationToken);
        var recipientParty = await altinnRegisterService.LookUpPartyById(correspondence.Recipient, cancellationToken);
        var senderPartyUuid = senderParty?.PartyUuid;
        var recipientPartyUuid = recipientParty?.PartyUuid;
        bool hasDialogportenDialog = correspondence.ExternalReferences.Any(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId);
        logger.LogInformation("Correspondence {CorrespondenceId} has Dialogporten dialog: {HasDialog}", correspondenceId, hasDialogportenDialog);

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
        else if (!await IsCorrespondenceReadyForPublish(correspondence, senderPartyUuid.Value, cancellationToken))
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
        else if (!string.IsNullOrEmpty(recipientParty!.OrgNumber) && !await HasPartyRequiredRoles(recipientPartyUuid.Value, correspondence.IsConfidential, cancellationToken))
        {
            errorMessage = $"Recipient of {correspondenceId} lacks roles required to read correspondence. Consider sending physical mail to this recipient instead.";
        }
        CorrespondenceStatusEntity status;
        AltinnEventType eventType = AltinnEventType.CorrespondencePublished;

        return await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            if (errorMessage.Length > 0)
            {
                logger.LogError("Publish failed for correspondence {CorrespondenceId}: {ErrorMessage}", correspondenceId, errorMessage);
                status = new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Failed,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = errorMessage,
                    PartyUuid = senderPartyUuid ?? Guid.Empty
                };
                var slackSent = await SlackHelper.SendSlackNotificationWithMessage("Correspondence failed", errorMessage, slackClient, slackSettings.NotificationChannel, hostEnvironment.EnvironmentName);
                if (!slackSent)
                {
                    logger.LogError("Failed to send Slack notification for failed correspondence {CorrespondenceId}: {ErrorMessage}", correspondenceId, errorMessage);
                }
                eventType = AltinnEventType.CorrespondencePublishFailed;
                logger.LogInformation("Cancelling notifications for failed correspondence {CorrespondenceId}", correspondenceId);
                var cancelNotificationJob = backgroundJobClient.Enqueue<CancelNotificationHandler>(handler => handler.Process(null, correspondenceId, null, cancellationToken));
                if (hasDialogportenDialog)
                {
                    logger.LogInformation("Purging Dialogporten dialog for failed correspondence {CorrespondenceId}", correspondenceId);
                    backgroundJobClient.ContinueJobWith<IDialogportenService>(cancelNotificationJob, dialogportenService => dialogportenService.PurgeCorrespondenceDialog(correspondenceId));
                }
            }
            else
            {
                logger.LogInformation("Publishing correspondence {CorrespondenceId}", correspondenceId);
                status = new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Published,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = CorrespondenceStatus.Published.ToString(),
                    PartyUuid = senderPartyUuid ?? Guid.Empty
                };
                await correspondenceRepository.UpdatePublished(correspondenceId, status.StatusChanged, cancellationToken);
                backgroundJobClient.Enqueue<ProcessLegacyPartyHandler>((handler) => handler.Process(correspondence.Recipient, null, cancellationToken));
                backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.ServiceOwner, DialogportenTextType.CorrespondencePublished, operationTimestamp));
            }

            await correspondenceStatusRepository.AddCorrespondenceStatus(status, cancellationToken);
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
            if (status.Status == CorrespondenceStatus.Published)
            {
                backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, CancellationToken.None));
            }
            logger.LogInformation("Successfully completed publish process for correspondence {CorrespondenceId} with status {Status}", correspondenceId, status.Status);
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }

    private async Task<bool> IsCorrespondenceReadyForPublish(CorrespondenceEntity correspondence, Guid partyUuid, CancellationToken cancellationToken)
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
                        StatusChanged = DateTime.UtcNow,
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

    private async Task<bool> HasPartyRequiredRoles(Guid partyUuid, bool isConfidential, CancellationToken cancellationToken)
    {
        var roles = await altinnRegisterService.LookUpPartyRoles(partyUuid.ToString(), cancellationToken);
        return roles.Any(r => (isConfidential
            ? ApplicationConstants.RequiredOrganizationRolesForConfidentialCorrespondenceRecipient
            : ApplicationConstants.RequiredOrganizationRolesForCorrespondenceRecipient)
            .Contains(r.Role.Identifier));
    }
}