using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceNotificationEventHandler(
    ICorrespondenceRepository correspondenceRepository,
    CorrespondenceMigrationEventHelper correspondenceMigrationEventHelper,
    ILogger<SyncCorrespondenceNotificationEventHandler> logger) : IHandler<SyncCorrespondenceNotificationEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceNotificationEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceByIdForSync(
            request.CorrespondenceId,
            CorrespondenceSyncType.NotificationEvents,
            cancellationToken);

        if (correspondence == null)
        {
            logger.LogError("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        // Use common helper method to filter duplicate events
        var notificationsToExecute = correspondenceMigrationEventHelper.FilterNotificationEvents(
            request.CorrespondenceId,
            request.SyncedEvents,
            correspondence);

        if (notificationsToExecute.Count == 0)
        {
            logger.LogInformation("No new notification events to sync for correspondence {CorrespondenceId}", request.CorrespondenceId);
            return request.CorrespondenceId;
        }

        // Use common helper method to process and save notification events
        await TransactionWithRetriesPolicy.Execute((cancellationToken) =>
        {
            return correspondenceMigrationEventHelper.ProcessNotificationEvents(
                request.CorrespondenceId,
                notificationsToExecute,
                MigrationOperationType.Sync,
                cancellationToken);
        }, logger, cancellationToken);

        return request.CorrespondenceId;
    }
}
