using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Persistence;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Text.Json;

namespace Altinn.Correspondence.Application.CheckNotificationDelivery;

public class CheckNotificationDeliveryHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    IAltinnNotificationService altinnNotificationService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CheckNotificationDeliveryHandler> logger,
    ApplicationDbContext dbContext)
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
        return await Process(notificationId, cancellationToken, publishFailedEvent: true);
    }

    public async Task<OneOf<bool, Error>> Process(Guid notificationId, CancellationToken cancellationToken, bool publishFailedEvent)
    {
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

            if (IsFinalOrderStatus(notification.NotificationOrderStatus))
            {
                logger.LogInformation("Notification {NotificationId} already processed with final order status {Status}",
                    notificationId, notification.NotificationOrderStatus);
                return true;
            }

            if (notification.ShipmentId == null)
            {
                logger.LogWarning("Notification {NotificationId} has no shipment ID", notificationId);
                return false;
            }

            // Check V2 notification delivery status
            logger.LogInformation("Checking V2 notification delivery status for shipment {ShipmentId}", notification.ShipmentId);
            var notificationDetailsV2 = await altinnNotificationService.GetNotificationDetailsV2(notification.ShipmentId.ToString()!, cancellationToken);
            
            if (notificationDetailsV2 == null)
            {
                logger.LogError("Failed to get notification details for shipment {ShipmentId}", notification.ShipmentId);
                return NotificationErrors.NotificationDetailsNotFound;
            }

            if (IsFinalOrderStatus(notificationDetailsV2.Status))
            {
                var failedRecipients = notificationDetailsV2.Recipients.Where(r => r.Status.IsFailed()).ToList();
                var sentRecipients = notificationDetailsV2.Recipients.Where(r => r.IsSent()).ToList();
                var allFailed = failedRecipients.Any() && notificationDetailsV2.Recipients.All(r => r.Status.IsFailed());
                var isMainOrder = IsMainOrder(notification, correspondence.Recipient);

                await DatabaseTransactionHelper.ExecuteAsync(dbContext, async (transactionToken) =>
                {
                    await correspondenceNotificationRepository.UpdateNotificationStatus(notificationId, notificationDetailsV2.Status, transactionToken);

                    if (failedRecipients.Any())
                    {
                        logger.LogError("Notification {NotificationId} has failed status (allFailed: {AllFailed}, isMainOrder: {IsMainOrder})", notificationId, allFailed, isMainOrder);
                        if (publishFailedEvent)
                        {
                            if (allFailed && isMainOrder)
                            {
                                SendAllFailedEvent(correspondence.ResourceId, correspondence.Id.ToString(), correspondence.Sender);
                            }
                            else
                            {
                                SendFailedEvent(correspondence.ResourceId, correspondence.Id.ToString(), correspondence.Sender);
                            }
                        }

                        foreach (var recipient in failedRecipients)
                        {
                            var failureTextType = recipient.Status.IsTtlFailure()
                                ? (notification.IsReminder ? DialogportenTextType.NotificationReminderDeliveryUnconfirmed : DialogportenTextType.NotificationDeliveryUnconfirmed)
                                : (notification.IsReminder ? DialogportenTextType.NotificationReminderFailed : DialogportenTextType.NotificationFailed);
                            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) =>
                                dialogportenService.CreateInformationActivity(
                                    correspondence.Id,
                                    DialogportenActorType.ServiceOwner,
                                    failureTextType,
                                    recipient.LastUpdate,
                                    recipient.Destination,
                                    recipient.Type.ToString()));
                        }
                    }

                    if (sentRecipients.Any())
                    {
                        var deliveryDestination = string.Join(", ", sentRecipients.Select(r => r.Destination));
                        var sentTime = sentRecipients.Min(r => r.LastUpdate);
                        await correspondenceNotificationRepository.UpdateNotificationSent(notificationId, sentTime, deliveryDestination, transactionToken);

                        var sentTextType = notification.IsReminder ? DialogportenTextType.NotificationReminderSent : DialogportenTextType.NotificationSent;
                        foreach (var recipient in sentRecipients)
                        {
                            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) =>
                                dialogportenService.CreateInformationActivity(
                                    correspondence.Id,
                                    DialogportenActorType.ServiceOwner,
                                    sentTextType,
                                    recipient.LastUpdate,
                                    recipient.Destination,
                                    recipient.Type.ToString()));
                        }

                        var sentEventType = notification.IsReminder
                            ? AltinnEventType.CorrespondenceNotificationReminderDelivered
                            : AltinnEventType.CorrespondenceNotificationDelivered;
                        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(
                            sentEventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
                    }

                    return true;
                }, cancellationToken);

                return true;
            }
            else
            {
                throw new InvalidOperationException($"Notification {notificationId} not yet finished (order status: {notificationDetailsV2.Status}). Throwing to retry.");
            }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking delivery status for notification {NotificationId}", notificationId);
                throw;
        }
    }

    private static readonly string[] FinalOrderStatuses = ["Order_Completed", "Order_SendConditionNotMet", "Order_Cancelled"];

    private static bool IsFinalOrderStatus(string? status) => status is not null && FinalOrderStatuses.Contains(status);

    private static bool IsMainOrder(CorrespondenceNotificationEntity notification, string correspondenceRecipient)
    {
        if (notification.OrderRequest == null) return true;
        var order = JsonSerializer.Deserialize<NotificationOrderRequestV2>(notification.OrderRequest);
        if (order == null) return true;
        var recipientWithoutPrefix = correspondenceRecipient.WithoutPrefix();
        var r = order.Recipient;
        if (r.RecipientOrganization != null) return r.RecipientOrganization.OrgNumber == recipientWithoutPrefix;
        if (r.RecipientPerson != null) return r.RecipientPerson.NationalIdentityNumber == recipientWithoutPrefix;
        if (r.RecipientExternalIdentity != null) return r.RecipientExternalIdentity.ExternalIdentity == correspondenceRecipient;
        return false;
    }

    private void SendFailedEvent(string resourceId, string correspondenceId, string sender)
    {
        logger.LogInformation("Enqueuing CorrespondenceNotificationFailed event for correspondence {CorrespondenceId}", correspondenceId);
        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceNotificationFailed, resourceId, correspondenceId, "correspondence", sender, CancellationToken.None));
    }

    private void SendAllFailedEvent(string resourceId, string correspondenceId, string sender)
    {
        logger.LogInformation("Enqueuing CorrespondenceNotificationAllFailed event for correspondence {CorrespondenceId}", correspondenceId);
        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceNotificationAllFailed, resourceId, correspondenceId, "correspondence", sender, CancellationToken.None));
    }
}
