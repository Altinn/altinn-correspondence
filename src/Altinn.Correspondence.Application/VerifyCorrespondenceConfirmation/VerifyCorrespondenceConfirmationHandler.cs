using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Application.VerifyCorrespondenceConfirmation;

/// <summary>
/// Background job that verifies Dialogporten is patched correctly and then persists Confirmed status + schedules side-effects.
/// </summary>
public class VerifyCorrespondenceConfirmationHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    ILogger<VerifyCorrespondenceConfirmationHandler> logger)
{
    public async Task VerifyPatchAndCommitConfirmation(
        Guid correspondenceId,
        Guid partyUuid,
        int partyId,
        DateTimeOffset operationTimestamp,
        string? callerPartyUrn,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Verifying Dialogporten patch and committing confirmation for correspondence {CorrespondenceId}", correspondenceId);

        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, false, cancellationToken);
        if (correspondence is null)
        {
            throw new Exception($"Correspondence {correspondenceId} not found for verifying confirmation");
        }

        if (correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
        {
            logger.LogInformation("Correspondence {CorrespondenceId} already confirmed; skipping verify+commit.", correspondenceId);
            return;
        }

        bool isDialogConfirmed = await dialogportenService.VerifyCorrespondenceDialogPatchedToConfirmed(correspondence.Id, cancellationToken);
        if (!isDialogConfirmed)
        {
            logger.LogWarning("Dialog not yet patched to confirmed for correspondence {CorrespondenceId}", correspondenceId);
            throw new Exception($"Dialog not patched to confirmed for correspondence {correspondenceId}.");
        }

        await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondence.Id,
                Status = CorrespondenceStatus.Confirmed,
                StatusChanged = operationTimestamp,
                StatusText = CorrespondenceStatus.Confirmed.ToString(),
                PartyUuid = partyUuid
            }, cancellationToken);

            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(
                AltinnEventType.CorrespondenceReceiverConfirmed,
                correspondence.ResourceId,
                correspondence.Id.ToString(),
                "correspondence",
                correspondence.Sender,
                CancellationToken.None));

            if (correspondence.Altinn2CorrespondenceId.HasValue && correspondence.Altinn2CorrespondenceId > 0)
            {
                backgroundJobClient.Enqueue<IAltinnStorageService>((syncToAltinn2) =>
                    syncToAltinn2.SyncCorrespondenceEventToSblBridge(
                        correspondence.Altinn2CorrespondenceId.Value,
                        partyId,
                        operationTimestamp,
                        SyncEventType.Confirm,
                        CancellationToken.None));
            }

            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) =>
                dialogportenService.CreateConfirmedActivity(correspondence.Id, DialogportenActorType.Recipient, operationTimestamp, callerPartyUrn));

            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}


