using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
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
    [AutomaticRetry(
        Attempts = 10,
        DelaysInSeconds = new[] 
        {
            60,
            15 * 60,
            4 * 60 * 60,
            8 * 60 * 60,
            12 * 60 * 60,
            16 * 60 * 60,
            36 * 60 * 60,
            48 * 60 * 60,
            72 * 60 * 60
        }
    )]
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
                var successfullyUpdated = await TransactionWithRetriesPolicy.RetryPolicy(logger).ExecuteAndCaptureAsync<bool>(
                    async (cancellationToken) => {
                        logger.LogInformation("Updating notification {NotificationId} as sent at {SentTime} to {Destinations}",
                            notificationId, sentTime, deliveryDestination);
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
                    }, cancellationToken);
                if (successfullyUpdated.Outcome == Polly.OutcomeType.Successful && successfullyUpdated.Result)
                {
                    return true;
                }
                else
                {
                    logger.LogError("Failed to update notification {NotificationId} as sent", notificationId);
                    throw new Exception("Failed to update notification as sent");
                }
            }
            
            logger.LogWarning("Notification {NotificationId} not yet sent", notificationId);
            if (correspondence.StatusHasBeen(Core.Models.Enums.CorrespondenceStatus.Read))
            {
                logger.LogInformation("Correspondence has been read. Hence no notification was sent");
                return true;
            }
            throw new InvalidOperationException("Notification not yet sent. Throwing to retry.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking delivery status for notification {NotificationId}", notificationId);
            throw;
        }
    }
}