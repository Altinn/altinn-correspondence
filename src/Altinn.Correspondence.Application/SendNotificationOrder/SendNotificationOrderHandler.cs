using System.Text.Json;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Application.CheckNotificationDelivery;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.SendNotificationOrder;

public class SendNotificationOrderHandler(
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    ICorrespondenceRepository correspondenceRepository,
    IAltinnNotificationService altinnNotificationService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SendNotificationOrderHandler> logger)
{
    private const int NotificationDeliveryCheckDelayMinutes = 5;

    public async Task Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting notification sending process for correspondence {CorrespondenceId}", correspondenceId);
        var notificationOrders = await correspondenceNotificationRepository.GetPrimaryNotificationsByCorrespondenceId(correspondenceId, cancellationToken);
        if (notificationOrders.Count == 0)
        {
            logger.LogInformation("No pending notification orders found for correspondence {CorrespondenceId}", correspondenceId);
            return;
        }
        logger.LogInformation("Sending {Count} notification request(s) V2 to notification service for correspondence {CorrespondenceId}", notificationOrders.Count, correspondenceId);

        await SendNotificationOrders(notificationOrders, correspondenceId, cancellationToken);
    }

    private async Task SendNotificationOrders(List<CorrespondenceNotificationEntity> notificationOrders, Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogError("Correspondence not found for correspondence {CorrespondenceId} when sending notification orders", correspondenceId);
            throw new Exception($"The correspondence {correspondenceId} was not found");
        }

        var allSuccessful = true;
        foreach (var notificationOrder in notificationOrders)
        {
            if (notificationOrder.OrderRequest == null)
            {
                logger.LogError("No order request found for notification order {NotificationOrderId}, when attempting to send notification order for correspondence {CorrespondenceId}", notificationOrder.Id, correspondenceId);
                throw new InvalidOperationException($"The correspondence notification {notificationOrder.Id} is missing an order request - unable to send notification order");
            }
            var orderRequest = JsonSerializer.Deserialize<NotificationOrderRequestV2>(notificationOrder.OrderRequest);
            if (orderRequest == null)
            {
                logger.LogError("Failed to deserialize order request for notification order {NotificationOrderId}", notificationOrder.Id);
                throw new InvalidOperationException($"The correspondence notification {notificationOrder.Id} has an invalid order request - unable to send notification order");
            }

            var successful = await SendNotificationOrder(notificationOrder, orderRequest, correspondence, cancellationToken);
            if (!successful)
            {
                allSuccessful = false;
            }
        }
        if (!allSuccessful)
        {
            SendFailedEvent(correspondence.ResourceId, correspondenceId.ToString(), correspondence.Sender);
        }
    }

    private async Task<bool> SendNotificationOrder(
        CorrespondenceNotificationEntity notificationOrder,
        NotificationOrderRequestV2 orderRequest,
        CorrespondenceEntity correspondence,
        CancellationToken cancellationToken)
    {
        var successful = true;
        logger.LogInformation("Sending notification order {IdempotencyId} for correspondence {CorrespondenceId}", orderRequest.IdempotencyId, correspondence.Id);
        var notificationResponse = await altinnNotificationService.CreateNotificationV2(orderRequest, cancellationToken);
        
        if (notificationResponse is null)
        {
            logger.LogError("Failed to create notification V2 for correspondence {CorrespondenceId}", correspondence.Id);
            successful = false;
        }
        else
        {
            await UpdateDatabaseNotificationOrder(notificationOrder, notificationResponse, cancellationToken);
            SendPublishedEvent(correspondence.ResourceId, notificationResponse.NotificationOrderId.ToString(), correspondence.Sender);
            logger.LogInformation("Scheduling notification delivery check for main notification {NotificationId}", notificationOrder.Id);
            ScheduleNotificationDeliveryCheck(notificationOrder, cancellationToken);
            if (orderRequest.Reminders != null && orderRequest.Reminders.Any())
            {
                foreach (var reminderResponse in notificationResponse.Notification.Reminders)
                {
                    var reminderNotification = await StoreReminderNotificationInDatabase(
                        notificationOrder,
                        orderRequest,
                        notificationResponse.NotificationOrderId,
                        reminderResponse,
                        cancellationToken);
                    logger.LogInformation("Scheduling notification delivery check for reminder notification {NotificationId}", reminderNotification.Id);
                    ScheduleNotificationDeliveryCheck(reminderNotification, cancellationToken);
                }
            }
        }

        return successful;
    }

    private async Task UpdateDatabaseNotificationOrder(CorrespondenceNotificationEntity notificationOrder, NotificationOrderRequestResponseV2 notificationResponse, CancellationToken cancellationToken)
    {
        notificationOrder.NotificationOrderId = notificationResponse.NotificationOrderId;
        notificationOrder.ShipmentId = notificationResponse.Notification.ShipmentId;
        await correspondenceNotificationRepository.UpdateOrderResponseData(notificationOrder.Id, notificationResponse.NotificationOrderId, notificationResponse.Notification.ShipmentId, cancellationToken);
    }

    private void ScheduleNotificationDeliveryCheck(CorrespondenceNotificationEntity notificationOrder, CancellationToken cancellationToken)
    {
        backgroundJobClient.Schedule<CheckNotificationDeliveryHandler>(
            handler => handler.Process(notificationOrder.Id, CancellationToken.None),
            notificationOrder.RequestedSendTime.AddMinutes(NotificationDeliveryCheckDelayMinutes));
    }

    private void SendPublishedEvent(string resourceId, string orderId, string sender) 
    {
        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.NotificationCreated, resourceId, orderId, "notification", sender, CancellationToken.None));
    }

    private void SendFailedEvent(string resourceId, string correspondenceId, string sender) 
    {
        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceNotificationCreationFailed, resourceId, correspondenceId, "correspondence", sender, CancellationToken.None));
    }

    private async Task<CorrespondenceNotificationEntity> StoreReminderNotificationInDatabase(
        CorrespondenceNotificationEntity mainNotificationOrder,
        NotificationOrderRequestV2 orderRequest,
        Guid notificationOrderId,
        ReminderResponse reminderResponse,
        CancellationToken cancellationToken)
    {
        var reminderNotification = new CorrespondenceNotificationEntity
        {
            Created = DateTimeOffset.UtcNow,
            NotificationTemplate = mainNotificationOrder.NotificationTemplate,
            NotificationChannel = mainNotificationOrder.NotificationChannel,
            CorrespondenceId = mainNotificationOrder.CorrespondenceId,
            RequestedSendTime = mainNotificationOrder.RequestedSendTime.AddDays(orderRequest.Reminders?.FirstOrDefault()?.DelayDays ?? 0),
            IsReminder = true,
            ShipmentId = reminderResponse.ShipmentId,
            NotificationOrderId = notificationOrderId,
            OrderRequest = JsonSerializer.Serialize(orderRequest)
        };
        await correspondenceNotificationRepository.AddNotification(reminderNotification, cancellationToken);

        return reminderNotification;
    }
}