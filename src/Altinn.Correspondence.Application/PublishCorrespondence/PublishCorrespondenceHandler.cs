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
using Altinn.Correspondence.Core.Models.Brreg;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Application.Settings;

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
    IBrregService brregService,
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
        var party = await altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for sender {Sender}", correspondence.Sender);
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        bool hasDialogportenDialog = correspondence.ExternalReferences.Any(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId);
        logger.LogInformation("Correspondence {CorrespondenceId} has Dialogporten dialog: {HasDialog}", correspondenceId, hasDialogportenDialog);

        OrganizationDetails? details = null;
        OrganizationRoles? roles = null;
        bool OrganizationNotFoundInBrreg = false;
        if (correspondence.GetRecipientUrn().WithoutPrefix().IsOrganizationNumber())
        {
            (details, roles, OrganizationNotFoundInBrreg) = await GetOrganizationDetailsAndRoles(correspondence, cancellationToken);
        }

        var errorMessage = "";
        if (correspondence == null)
        {
            errorMessage = "Correspondence " + correspondenceId + " not found when publishing";
            logger.LogError(errorMessage);
        }
        else if (!await IsCorrespondenceReadyForPublish(correspondence, partyUuid, cancellationToken))
        {
            errorMessage = $"Correspondence {correspondenceId} not ready for publish";
        }
        else if (!hasDialogportenDialog)
        {
            errorMessage = $"Dialogporten dialog not created for correspondence {correspondenceId}";
            logger.LogError(errorMessage);
        }
        else if (await HasRecipientBeenSetToReservedInKRR(correspondence, cancellationToken))
        {
            errorMessage = $"Recipient of {correspondenceId} has been set to reserved in kontakt- og reserverasjonsregisteret ('KRR')";
        }
        else if (OrganizationNotFoundInBrreg)
        {
            errorMessage = $"Recipient of {correspondenceId} is not found in 'Enhetsregisteret'";
        }
        else if (details != null && details.IsBankrupt)
        {
            errorMessage = $"Recipient of {correspondenceId} is bankrupt";
        }
        else if (details != null && details.IsDeleted)
        {
            errorMessage = $"Recipient of {correspondenceId} is deleted";
        }
        else if (roles != null && !roles.HasAnyOfRolesOnPerson(
            correspondence.IsConfidential
                ? ApplicationConstants.RequiredOrganizationRolesForConfidentialCorrespondenceRecipient
                : ApplicationConstants.RequiredOrganizationRolesForCorrespondenceRecipient))
        {
            errorMessage = $"Recipient of {correspondenceId} lacks roles required to read correspondences. Consider sending physical mail to this recipient instead.";
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
                    PartyUuid = partyUuid
                };
                var slackSent = await SlackHelper.SendSlackNotificationWithMessage("Correspondence failed", errorMessage, slackClient, slackSettings.NotificationChannel, hostEnvironment.EnvironmentName);
                if (!slackSent)
                {
                    logger.LogError("Failed to send Slack notification for failed correspondence {CorrespondenceId}: {ErrorMessage}", correspondenceId, errorMessage);
                }
                eventType = AltinnEventType.CorrespondencePublishFailed;
                logger.LogInformation("Cancelling notifications for failed correspondence {CorrespondenceId}", correspondenceId);
                backgroundJobClient.Enqueue<CancelNotificationHandler>(handler => handler.Process(null, correspondenceId, null, cancellationToken));
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
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = CorrespondenceStatus.Published.ToString(),
                    PartyUuid = partyUuid
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

    private async Task<(OrganizationDetails?, OrganizationRoles?, bool)> GetOrganizationDetailsAndRoles(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        OrganizationDetails? details = null;
        OrganizationRoles? roles = null;
        bool OrganizationNotFoundInBrreg = false;
        if (correspondence.GetRecipientUrn().WithoutPrefix().IsOrganizationNumber())
        {
            try
            {
                details = await brregService.GetOrganizationDetails(correspondence.Recipient.WithoutPrefix(), cancellationToken);
                roles = await brregService.GetOrganizationRoles(correspondence.Recipient.WithoutPrefix(), cancellationToken);
            }
            catch (BrregNotFoundException)
            {
                try
                {
                    var subOrganizationDetails = await brregService.GetSubOrganizationDetails(correspondence.Recipient.WithoutPrefix(), cancellationToken);
                    details = subOrganizationDetails;
                    if (subOrganizationDetails.ParentOrganizationNumber != null)
                    {
                        roles = await brregService.GetOrganizationRoles(subOrganizationDetails.ParentOrganizationNumber, cancellationToken);
                    }
                    else
                    {
                        roles = new OrganizationRoles();
                    }
                }
                catch (BrregNotFoundException)
                {
                    OrganizationNotFoundInBrreg = true;
                }
            }
        }
        return (details, roles, OrganizationNotFoundInBrreg);
    }
}