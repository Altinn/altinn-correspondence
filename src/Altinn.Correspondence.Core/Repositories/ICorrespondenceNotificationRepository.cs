using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceNotificationRepository
    {
        Task<Guid> AddNotification(CorrespondenceNotificationEntity notification, CancellationToken cancellationToken);
        Task<Guid> AddNotificationForSync(CorrespondenceNotificationEntity notification, CancellationToken cancellationToken);
        Task<CorrespondenceNotificationEntity?> GetPrimaryNotification(Guid correspondenceId, CancellationToken cancellationToken);
        Task<CorrespondenceNotificationEntity?> GetNotificationById(Guid notificationId, CancellationToken cancellationToken);
        Task UpdateNotificationSent(Guid notificationId, DateTimeOffset sentTime, string destination, CancellationToken cancellationToken);
        Task UpdateOrderResponseData(Guid notificationId, Guid notificationOrderId, Guid shipmentId, CancellationToken cancellationToken);
        Task WipeOrder(Guid notificationId, CancellationToken cancellationToken);
        Task<List<CorrespondenceNotificationEntity>> GetPrimaryNotificationsByCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken);
        Task<List<NotificationDeliveryRepairCandidate>> GetAltinn3NotificationDeliveryRepairCandidates(
            DateTimeOffset requestedSendTimeOlderThan,
            Guid? afterNotificationId,
            int limit,
            CancellationToken cancellationToken);
    }
}