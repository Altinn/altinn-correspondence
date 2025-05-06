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
using Altinn.Correspondence.Integrations.Redlock;
using Altinn.Correspondence.Helpers;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using Slack.Webhooks;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.PublishCorrespondence;

public class PublishCorrespondenceHandler(
    IAltinnRegisterService altinnRegisterService,
    ILogger<PublishCorrespondenceHandler> logger,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IContactReservationRegistryService contactReservationRegistryService,
    IDialogportenService dialogportenService,
    IHostEnvironment hostEnvironment,
    ISlackClient slackClient,
    SlackSettings slackSettings,
    IBackgroundJobClient backgroundJobClient,
    HybridDistributedLockHelper hybridDistributedLockHelper) : IHandler<Guid, Task>
{
    private const string CorrespondenceStatusLockKeyPrefix = "correspondence_status_lock_";
    
    public async Task<OneOf<Task, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        // Hybrid distributed lock with conditional check is used to prevent race conditions
        string lockKey = CorrespondenceStatusLockKeyPrefix + correspondenceId;
        
        OneOf<Task, Error> result = Task.CompletedTask;
        
        async Task ExecuteAction(CancellationToken ct)
        {
            result = await ProcessWithLock(correspondenceId, user, ct);
        }
        
        var (wasSkipped, lockAcquired) = await hybridDistributedLockHelper.ExecuteWithConditionalHybridLockAsync(
            lockKey,
            async (ct) => await IsAlreadyPublished(correspondenceId, ct),
            ExecuteAction,
            retryCount: 2,
            retryDelayMs: 100,
            cancellationToken: cancellationToken);

        if (!lockAcquired && !wasSkipped)
        {
            if (await IsAlreadyPublished(correspondenceId, cancellationToken))
            {
                logger.LogInformation("Could not acquire lock for correspondence {correspondenceId}, publish has been handled by another process", correspondenceId);
            }
            else
            {
                logger.LogError("Could not acquire lock for correspondence {correspondenceId}, publish may have been handled by another process", correspondenceId);
            }
        }
        
        return result;
    }

    private async Task<bool> IsAlreadyPublished(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        return correspondence?.GetHighestStatus()?.Status == CorrespondenceStatus.Published || correspondence?.GetHighestStatus()?.Status == CorrespondenceStatus.Failed;
    }
    
    private async Task<OneOf<Task, Error>> ProcessWithLock(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Publish correspondence {correspondenceId}", correspondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence?.GetHighestStatus()?.Status == CorrespondenceStatus.Published || correspondence?.GetHighestStatus()?.Status == CorrespondenceStatus.Failed)
        {
            return Task.CompletedTask;
        }
        var party = await altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        bool hasDialogportenDialog = correspondence.ExternalReferences.Any(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId);
        var errorMessage = "";
        if (correspondence == null)
        {
            errorMessage = "Correspondence " + correspondenceId + " not found when publishing";
        }
        else if (hostEnvironment.IsDevelopment() && correspondence.StatusHasBeen(CorrespondenceStatus.Published))
        {
            return Task.CompletedTask;
        }
        else if (correspondence.GetHighestStatus()?.Status != CorrespondenceStatus.ReadyForPublish)
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
                errorMessage = $"Correspondence {correspondenceId} not ready for publish";
            }
        }
        else if (correspondence.RequestedPublishTime > DateTimeOffset.UtcNow)
        {
            errorMessage = $"Correspondence {correspondenceId} not visible yet";
        }
        else if (!hasDialogportenDialog)
        {
            errorMessage = $"Dialogporten dialog not created for correspondence {correspondenceId}";
        }
        else if (correspondence.IgnoreReservation != true && correspondence.GetRecipientUrn().IsSocialSecurityNumber())
        {
            var isReserved = await contactReservationRegistryService.IsPersonReserved(correspondence.GetRecipientUrn().WithoutPrefix());
            if (isReserved)
            {
                errorMessage = $"Recipient of {correspondenceId} has been set to reserved in kontakt- og reserverasjonsregisteret ('KRR')";
            }
        }
        CorrespondenceStatusEntity status;
        AltinnEventType eventType = AltinnEventType.CorrespondencePublished;

        return await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
        {
            if (errorMessage.Length > 0)
            {
                logger.LogError(errorMessage);
                status = new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Failed,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = errorMessage,
                    PartyUuid = partyUuid
                };
                var slackSent = await SlackHelper.SendSlackNotificationWithMessage("Correspondence failed", errorMessage, slackClient, slackSettings.NotificationChannel, hostEnvironment.EnvironmentName);
                if (!slackSent)
                {
                    logger.LogError($"Failed to send Slack notification for failed correspondence: {errorMessage}");
                }
                eventType = AltinnEventType.CorrespondencePublishFailed;
                foreach (var notification in correspondence.Notifications)
                {
                    backgroundJobClient.Enqueue<CancelNotificationHandler>(handler => handler.Process(null, correspondenceId, null, cancellationToken));
                }
                if (hasDialogportenDialog)
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(dialogportenService => dialogportenService.PurgeCorrespondenceDialog(correspondenceId));
                }
            }
            else
            {
                status = new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Published,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = CorrespondenceStatus.Published.ToString(),
                    PartyUuid = partyUuid
                };
                await correspondenceRepository.UpdatePublished(correspondenceId, status.StatusChanged, cancellationToken);
                backgroundJobClient.Enqueue<ProcessLegacyPartyHandler>((handler) => handler.Process(correspondence.Recipient, null, cancellationToken));
                backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.ServiceOwner, DialogportenTextType.CorrespondencePublished));
            }

            await correspondenceStatusRepository.AddCorrespondenceStatus(status, cancellationToken);
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
            if (status.Status == CorrespondenceStatus.Published) backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, CancellationToken.None));
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}