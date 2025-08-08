using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using System.Linq;
using Slack.Webhooks.Elements;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceStatusEventHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper,
    SyncCorrespondenceStatusHelper syncCorrespondenceStatusHelper,
    ILogger<SyncCorrespondenceStatusEventHandler> logger) : IHandler<SyncCorrespondenceStatusEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceStatusEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Processing status Sync request for correspondence {request.CorrespondenceId} with {request.SyncedEvents.Count} # of status events");

        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, false, cancellationToken, true);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        var eventsFilteredForDuplicates = new List<CorrespondenceStatusEntity>();

        foreach (var statusEventToSync in request.SyncedEvents)
        {
            bool existsAlready = false;

            foreach(var statusEventInAltinn3 in correspondence.Statuses)
            {
                // IDempotent Key == CorrespondenceId + Status + StatusChanged + PartyUuid
                if (statusEventToSync.Status == statusEventInAltinn3.Status &&
                    statusEventToSync.StatusChanged.EqualsToWithinSecond(statusEventInAltinn3.StatusChanged) && // Only compare to nearest second
                    statusEventToSync.PartyUuid == statusEventInAltinn3.PartyUuid)
                {
                    existsAlready = true;                    
                }
            }

            if(existsAlready)
            {
                logger.LogInformation($"Current Status Event for {request.CorrespondenceId} has been deemed duplicate of existing and will be ignored. Status: {statusEventToSync.Status}- StatusChanged: {statusEventToSync.StatusChanged}- PartyUuid: {statusEventToSync.PartyUuid}");
            }
            else
            {
                eventsFilteredForDuplicates.Add(statusEventToSync);
            }
        }

        if (eventsFilteredForDuplicates.Count > 0)
        {
            logger.LogInformation($"Executing status synctransaction for correspondence for {request.CorrespondenceId} with {request.SyncedEvents.Count} # of status events");

            // Special case for Purge events, we need to handle them differently
            var purgeEvent = eventsFilteredForDuplicates
            .FirstOrDefault(e =>
                e.Status == Core.Models.Enums.CorrespondenceStatus.PurgedByRecipient ||
                e.Status == Core.Models.Enums.CorrespondenceStatus.PurgedByAltinn);
            if (purgeEvent != null)
            {
                var alreadyPurged = correspondence.GetPurgedStatus();
                if (alreadyPurged is not null)
                {
                    logger.LogInformation($"Purge Event received, but Correspondence has already been purged, so skipping action: {request.CorrespondenceId}");
                }
                else
                {
                    logger.LogInformation($"Purge Correspondence based on sync Event from Altinn 2: {request.CorrespondenceId}");
                    await syncCorrespondenceStatusHelper.PurgeCorrespondence(correspondence, purgeEvent, cancellationToken);
                }
                eventsFilteredForDuplicates.Remove(purgeEvent);
            }

            // Handle the rest of the status events collectively
            if (eventsFilteredForDuplicates.Count > 0)
            {
                await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
                {
                    await syncCorrespondenceStatusHelper.AddSyncedCorrespondenceStatuses(correspondence, eventsFilteredForDuplicates, cancellationToken);

                    if (correspondence.IsMigrating == false)
                    {
                        foreach (var eventToExecute in eventsFilteredForDuplicates)
                        {
                            updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, eventToExecute.Status, eventToExecute.StatusChanged); // Set the operationtime to the time the status was changed in Altinn 2
                            updateCorrespondenceStatusHelper.PatchCorrespondenceDialog(request.CorrespondenceId, eventToExecute.Status);
                            updateCorrespondenceStatusHelper.PublishEvent(correspondence, eventToExecute.Status);
                        }
                    }

                    return Task.CompletedTask;
                }, logger, cancellationToken);
            }
        }

        logger.LogInformation($"Successfully synced request for correspondence {request.CorrespondenceId} with {request.SyncedEvents.Count} # of status events");
        return request.CorrespondenceId;
    }
}


public static class DateTimeOffsetExtensions
{
    public static bool EqualsToWithinSecond(this DateTimeOffset dto1, DateTimeOffset dto2)
    {
        // Normalize to UTC to handle different offsets correctly
        DateTimeOffset utcDto1 = dto1.ToUniversalTime();
        DateTimeOffset utcDto2 = dto2.ToUniversalTime();

        // Truncate to the second by creating a new DateTimeOffset
        // with milliseconds, microseconds, and ticks set to zero.
        DateTimeOffset truncatedDto1 = new DateTimeOffset(
            utcDto1.Year, utcDto1.Month, utcDto1.Day,
            utcDto1.Hour, utcDto1.Minute, utcDto1.Second,
            TimeSpan.Zero // Set offset to zero for UTC
        );

        DateTimeOffset truncatedDto2 = new DateTimeOffset(
            utcDto2.Year, utcDto2.Month, utcDto2.Day,
            utcDto2.Hour, utcDto2.Minute, utcDto2.Second,
            TimeSpan.Zero // Set offset to zero for UTC
        );

        return truncatedDto1.Equals(truncatedDto2);
    }
}