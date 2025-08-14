using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceNotificationEventHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceNotificationRepository notificationRepository,    
    ILogger<SyncCorrespondenceNotificationEventHandler> logger) : IHandler<SyncCorrespondenceNotificationEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceNotificationEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {        
        if (request.SyncedEvents == null || !request.SyncedEvents.Any())
        {
            logger.LogError("No notification Events specified in request to sync for correspondence {CorrespondenceId}", request.CorrespondenceId);
            return SyncErrors.NoEventsToSync;
        }
       
        var correspondence = await correspondenceRepository.GetCorrespondenceById(
            request.CorrespondenceId,
            includeStatus: false,
            includeContent: false,
            includeForwardingEvents: false, 
            cancellationToken, 
            includeIsMigrating: true);
        if(correspondence == null)
        {
            logger.LogError("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        // Validate that Notification events are not duplicates
        var existingNotifications = correspondence.Notifications;

        var notificationsToExecute = new List<CorrespondenceNotificationEntity>();
        foreach (var syncedEvent in request.SyncedEvents)
        {
            if (existingNotifications.Any(n => n.NotificationAddress == syncedEvent.NotificationAddress && n.NotificationChannel == syncedEvent.NotificationChannel && n.NotificationSent == syncedEvent.NotificationSent))
            {
                logger.LogWarning("Notification event {NotificationId} already exists for correspondence {CorrespondenceId}. Skipping sync.", syncedEvent.Id, request.CorrespondenceId);
                continue; // Skip already existing events
            }
            else
            {
                notificationsToExecute.Add(syncedEvent);
            }
        }

        if (!notificationsToExecute.Any())
        {
            logger.LogInformation("No new notification events to sync for correspondence {CorrespondenceId}", request.CorrespondenceId);
            return request.CorrespondenceId; // No new events to sync
        }

        // Save the new Notificaitons to repository
        foreach (var notification in notificationsToExecute)
        {            
            notification.CorrespondenceId = request.CorrespondenceId;
            notification.SyncedFromAltinn2 = DateTimeOffset.UtcNow;
            var addedNotificationId = await notificationRepository.AddNotification(notification, cancellationToken);
            logger.LogInformation("Added new notification {NotificationId} for correspondence {CorrespondenceId}", addedNotificationId, request.CorrespondenceId);
        }

        return request.CorrespondenceId;
    }
}
