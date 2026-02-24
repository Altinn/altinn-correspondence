using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceStatusEventHandler(
ICorrespondenceRepository correspondenceRepository,
CorrespondenceMigrationEventHelper correspondenceMigrationEventHelper,
ILogger<SyncCorrespondenceStatusEventHandler> logger) : IHandler<SyncCorrespondenceStatusEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceStatusEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        int numSyncedEvents = request.SyncedEvents?.Count ?? 0;
        int numSyncedDeletes = request.SyncedDeleteEvents?.Count ?? 0;

        logger.LogInformation("Processing status Sync request for correspondence {CorrespondenceId} with {numSyncedEvents} status events and {numSyncedDeletes} delete events", 
            request.CorrespondenceId, numSyncedEvents, numSyncedDeletes);

        if (numSyncedEvents == 0 && numSyncedDeletes == 0)
        {
            logger.LogInformation("No events to sync for Correspondence {CorrespondenceId}. Exiting sync process.", request.CorrespondenceId);
            return request.CorrespondenceId;
        }

        var txResult = await TransactionWithRetriesPolicy.Execute<OneOf<Guid, Error>>(async (cancellationToken) =>
        {
            // Fetch correspondence with fresh data within transaction
            var correspondence = await correspondenceRepository.GetCorrespondenceByIdForSync(
                request.CorrespondenceId,
                CorrespondenceSyncType.StatusEvents,
                cancellationToken);

            if (correspondence == null)
            {
                logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
                return CorrespondenceErrors.CorrespondenceNotFound;
            }

            var statusEventsToProcess = new List<CorrespondenceStatusEntity>();
            var deletionEventsToProcess = new List<CorrespondenceDeleteEventEntity>();

            if (numSyncedEvents > 0)
            {
                statusEventsToProcess = correspondenceMigrationEventHelper.FilterStatusEvents(request.CorrespondenceId, request.SyncedEvents, correspondence);
                if (statusEventsToProcess.Count == 0)
                {
                    logger.LogWarning("None of the Status Events for {CorrespondenceId} were unique, and no sync will be performed.", request.CorrespondenceId);
                }
            }

            if (numSyncedDeletes > 0)
            {
                deletionEventsToProcess = await correspondenceMigrationEventHelper.FilterDeleteEvents(request.CorrespondenceId, request.SyncedDeleteEvents, cancellationToken);
                if (deletionEventsToProcess.Count == 0)
                {
                    logger.LogWarning("None of the Delete Events for {CorrespondenceId} were unique, and no sync will be performed.", request.CorrespondenceId);
                }
            }

            if (deletionEventsToProcess.Count == 0 && statusEventsToProcess.Count == 0)
            {
                logger.LogInformation("No unique Status or Delete Events to sync for Correspondence {CorrespondenceId}. Exiting sync process.", request.CorrespondenceId);
                return request.CorrespondenceId;
            }

            // Process all events using the shared helper method
            await correspondenceMigrationEventHelper.ProcessEventsInChronologicalOrder(
                request.CorrespondenceId,
                correspondence,
                statusEventsToProcess,
                deletionEventsToProcess,
                "sync",
                cancellationToken);

            return request.CorrespondenceId;
        }, logger, cancellationToken);

        return txResult.Match<OneOf<Guid, Error>>(
            success => 
            {
                logger.LogInformation("Successfully synced request for correspondence {CorrespondenceId} with {numSyncedEvents} status events and {numSyncedDeletes} delete events", 
                    request.CorrespondenceId, numSyncedEvents, numSyncedDeletes);
                return success;
            },
            error => 
            {
                logger.LogWarning("Failed to sync request for correspondence {CorrespondenceId}: {Error}", 
                    request.CorrespondenceId, error);
                return error;
            });
    }
}
