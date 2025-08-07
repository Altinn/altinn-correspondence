using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceStatusEventHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper,
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
        
        var currentStatusError = syncCorrespondenceStatusHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            logger.LogWarning("Current status validation failed for correspondence {CorrespondenceId}: {Error}",
                request.CorrespondenceId,
                currentStatusError);
            return currentStatusError;
        }

        var eventsToExecute = new List<CorrespondenceStatusEntity>();

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
                eventsToExecute.Add(statusEventToSync);
            }
        }

        if(eventsToExecute.Count > 0)
        {
            logger.LogInformation($"Executing status synctransaction for correspondence for {request.CorrespondenceId} with {request.SyncedEvents.Count} # of status events");           

            
            await TransactionWithRetriesPolicy.Execute<Task>(async (cancellationToken) =>
            {
                await syncCorrespondenceStatusHelper.AddSyncedCorrespondenceStatuses(correspondence, eventsToExecute, cancellationToken);

                if (correspondence.IsMigrating == false)
                {
                    foreach (var eventToExecute in eventsToExecute)
                    {
                        updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, eventToExecute.Status, eventToExecute.StatusChanged); // Set the operationtime to the time the status was changed in Altinn 2
                        updateCorrespondenceStatusHelper.PatchCorrespondenceDialog(request.CorrespondenceId, eventToExecute.Status);
                        updateCorrespondenceStatusHelper.PublishEvent(correspondence, eventToExecute.Status);
                    }
                }

                return Task.CompletedTask;
            }, logger, cancellationToken);
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