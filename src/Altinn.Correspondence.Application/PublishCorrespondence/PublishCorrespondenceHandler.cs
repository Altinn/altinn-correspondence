using Altinn.Correspondence.Application.CancelNotification;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.ProcessLegacyParty;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Hangfire;
using Microsoft.Extensions.Hosting;
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
    IEventBus eventBus,
    IHostEnvironment hostEnvironment,
    IBackgroundJobClient backgroundJobClient) : IHandler<Guid, Task>
{
    public async Task<OneOf<Task, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Publish correspondence {correspondenceId}", correspondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        var party = await altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
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
                eventType = AltinnEventType.CorrespondencePublishFailed;
                foreach (var notification in correspondence.Notifications)
                {
                    backgroundJobClient.Enqueue<CancelNotificationHandler>(handler => handler.Process(null, correspondenceId, null, cancellationToken));
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
            await eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
            if (status.Status == CorrespondenceStatus.Published) await eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken);
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}