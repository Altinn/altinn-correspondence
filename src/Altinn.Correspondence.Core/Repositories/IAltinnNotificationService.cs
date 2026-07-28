using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Notifications;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnNotificationService
{
    Task<NotificationOrderRequestResponseV2?> CreateNotificationV2(NotificationOrderRequestV2 notificationRequest, CancellationToken cancellationToken = default);
    Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default);
    Task<NotificationStatusResponse> GetNotificationDetails(string orderId, CancellationToken cancellationToken = default);
    Task<NotificationStatusResponseV2> GetNotificationDetailsV2(string shipmentId, CancellationToken cancellationToken = default);
    Task<ComposedEmailResponse> CreateComposedEmail(ComposedEmailRequest composedEmailRequest, CancellationToken cancellationToken = default);
}

