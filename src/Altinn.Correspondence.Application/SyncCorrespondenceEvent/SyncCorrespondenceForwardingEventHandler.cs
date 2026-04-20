using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceForwardingEventHandler(
ICorrespondenceRepository correspondenceRepository,
CorrespondenceMigrationEventHelper correspondenceMigrationEventHelper,
ILogger<SyncCorrespondenceForwardingEventHandler> logger) : IHandler<SyncCorrespondenceForwardingEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceForwardingEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceByIdForSync(
            request.CorrespondenceId,
            CorrespondenceSyncType.ForwardingEvents,
            cancellationToken);

        if (correspondence == null)
        {
            logger.LogError("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        // Use common helper method to filter duplicate events
        var forwardingEventsToExecute = correspondenceMigrationEventHelper.FilterForwardingEvents(
            request.CorrespondenceId,
            request.SyncedEvents,
            correspondence);

        if (forwardingEventsToExecute.Count == 0)
        {
            logger.LogInformation("No new forwarding events to sync for correspondence {CorrespondenceId}", request.CorrespondenceId);
            return request.CorrespondenceId;
        }

        // Process, save, and enqueue background jobs for forwarding events
        // Note: We don't use TransactionWithRetriesPolicy here because AddRangeAsync + SaveChangesAsync 
        // within a TransactionScope causes "operation in progress" errors with PostgreSQL.
        // EF Core's SaveChangesAsync() already provides transactional guarantees.
        await correspondenceMigrationEventHelper.ProcessForwardingEvents(
            request.CorrespondenceId,
            correspondence,
            forwardingEventsToExecute,
            MigrationOperationType.Sync,
            cancellationToken);

        return request.CorrespondenceId;
    }
}
