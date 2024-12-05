using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Logging;
using OneOf;
using Microsoft.Extensions.Hosting;
using Altinn.Correspondence.Application.CancelNotification;
using Hangfire;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.PublishCorrespondence;

public class PublishCorrespondenceHandler(
    IAltinnRegisterService altinnRegisterService,
    ILogger<PublishCorrespondenceHandler> logger,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IEventBus eventBus,
    IHostEnvironment hostEnvironment,
    IBackgroundJobClient backgroundJobClient) : IHandler<Guid, Task>
{
    public async Task<OneOf<Task, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Publish correspondence {correspondenceId}", correspondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        var errorMessage = "";
        if (correspondence == null)
        {
            errorMessage = "Correspondence " + correspondenceId + " not found when publishing";
        }
        else if (hostEnvironment.IsDevelopment() && correspondence.StatusHasBeen(CorrespondenceStatus.Published))
        {
            return Task.CompletedTask;
        }
        else if (correspondence.GetHighestStatus()?.Status != CorrespondenceStatus.ReadyForPublish) // TODO: Change to check if equal to initialized if/when ReadyForPublish is removed
        {
            errorMessage = $"Correspondence {correspondenceId} not ready for publish";
        }
        else if (correspondence.RequestedPublishTime > DateTimeOffset.UtcNow)
        {
            errorMessage = $"Correspondence {correspondenceId} not visible yet";
        }
        CorrespondenceStatusEntity status;
        AltinnEventType eventType = AltinnEventType.CorrespondencePublished;
        var party = await altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

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
                backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.ServiceOwner, DialogportenTextType.CorrespondencePublished));
            }

            await correspondenceStatusRepository.AddCorrespondenceStatus(status, cancellationToken);
            await eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
            if (status.Status == CorrespondenceStatus.Published) await eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken);
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}