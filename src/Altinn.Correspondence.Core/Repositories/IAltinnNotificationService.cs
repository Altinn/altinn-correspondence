using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnNotificationService
{
    Task<Guid?> CreateNotification(CorrespondenceEntity correspondence, NotificationOrderRequest notification, CancellationToken cancellationToken = default);

    Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default);
}

