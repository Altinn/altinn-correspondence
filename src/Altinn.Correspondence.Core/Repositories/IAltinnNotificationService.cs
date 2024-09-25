using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnNotificationService
{
    Task<Guid?> CreateNotification(NotificationOrderRequest notification, CancellationToken cancellationToken = default);

    Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default);

    Task<NotificationStatusResponse> GetNotificationDetails(string orderId, CancellationToken cancellationToken = default);
}

