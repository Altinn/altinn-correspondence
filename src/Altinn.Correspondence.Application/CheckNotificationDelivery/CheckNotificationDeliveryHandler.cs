using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.CheckNotificationDelivery;

public class CheckNotificationDeliveryHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    IAltinnNotificationService altinnNotificationService,
    IDialogportenService dialogportenService,
    ILogger<CheckNotificationDeliveryHandler> logger)
{
    public async Task<OneOf<bool, Error>> Process(Guid notificationId, CancellationToken cancellationToken)
    {
        var operationTimestamp = DateTimeOffset.UtcNow;
        logger.LogInformation("Checking delivery status for notification {NotificationId}", notificationId);
        
        var notification = await correspondenceNotificationRepository.GetNotificationById(notificationId, cancellationToken);
        if (notification == null)
        {
            logger.LogWarning("Notification {NotificationId} not found", notificationId);
            return NotificationErrors.NotificationNotFound;
        }

        var correspondence = await correspondenceRepository.GetCorrespondenceById(notification.CorrespondenceId, true, true, false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found for notification {NotificationId}", 
                notification.CorrespondenceId, notificationId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        logger.LogInformation("Processing notification {NotificationId} for correspondence {CorrespondenceId}", 
            notificationId, correspondence.Id);

        try
        {
            // Check if notification is already marked as sent
            if (notification.NotificationSent.HasValue)
            {
                logger.LogInformation("Notification {NotificationId} already marked as sent at {SentTime}", 
                    notificationId, notification.NotificationSent.Value);
                return true;
            }

            if (notification.ShipmentId == null)
            {
                logger.LogWarning("Notification {NotificationId} has no shipment ID", notificationId);
                return false;
            }

            // Check V2 notification delivery status
            logger.LogInformation("Checking V2 notification delivery status for shipment {ShipmentId}", notification.ShipmentId);
            var notificationDetailsV2 = await altinnNotificationService.GetNotificationDetailsV2(notification.ShipmentId.ToString(), cancellationToken);
            
            if (notificationDetailsV2 == null)
            {
                logger.LogError("Failed to get notification details for shipment {ShipmentId}", notification.ShipmentId);
                return NotificationErrors.NotificationDetailsNotFound;
            }

            var sentRecipients = notificationDetailsV2.Recipients
                .Where(r => r.IsSent())
                .ToList();

            if (sentRecipients.Any())
            {
                var deliveryDestination = string.Join(", ", sentRecipients.Select(r => r.Destination));
                var sentTime = sentRecipients.Min(r => r.LastUpdate);
                logger.LogInformation("Notification {NotificationId} sent to: {Destinations}", 
                    notificationId, deliveryDestination);

                // Mark notification as sent
                // Notification sent time is the time of the first recipient that was sent (last update)
                // According to Team Altinn Notification, last update reflects when we receive the delivery confirmation from the network operator.
                await correspondenceNotificationRepository.UpdateNotificationSent(notificationId, sentTime, deliveryDestination, cancellationToken);
                
                // Create activity in Dialogporten for each recipient
                // Choose the appropriate text type based on whether this is a reminder notification
                var textType = notification.IsReminder ? DialogportenTextType.NotificationReminderSent : DialogportenTextType.NotificationSent;
                
                foreach (var recipient in sentRecipients)
                {
                    await dialogportenService.CreateInformationActivity(
                        correspondence.Id, 
                        DialogportenActorType.ServiceOwner, 
                        textType,
                        operationTimestamp, 
                        recipient.Destination,
                        recipient.Type.ToString());
                }
                
                logger.LogInformation("Successfully processed sent notification {NotificationId} and created activities", notificationId);
                return true;
            }
            
            logger.LogWarning("Notification {NotificationId} not yet sent", notificationId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking delivery status for notification {NotificationId}", notificationId);
            throw;
        }
    }
}