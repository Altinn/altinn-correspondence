using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnNotificationService
{
    Task<NotificationOrderRequestResponse?> CreateNotification(NotificationOrderRequest notification, CancellationToken cancellationToken = default);
    Task<NotificationOrderRequestResponseV2?> CreateNotificationV2(NotificationOrderRequestV2 notificationRequest, CancellationToken cancellationToken = default);
    Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default);
    Task<NotificationStatusResponse> GetNotificationDetails(string orderId, CancellationToken cancellationToken = default);
}

