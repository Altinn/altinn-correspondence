using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceNotificationRepository
    {
        Task<Guid> AddNotification(CorrespondenceNotificationEntity notification, CancellationToken cancellationToken);
        Task<CorrespondenceNotificationEntity?> GetPrimaryNotification(Guid correspondenceId, CancellationToken cancellationToken);
        Task<CorrespondenceNotificationEntity?> GetNotificationById(Guid notificationId, CancellationToken cancellationToken);
        Task UpdateNotificationSent(Guid notificationId, DateTimeOffset sentTime, string destination, CancellationToken cancellationToken);
        Task UpdateOrderResponseData(Guid notificationId, Guid notificationOrderId, Guid shipmentId, CancellationToken cancellationToken);
        Task WipeOrder(Guid notificationId, CancellationToken cancellationToken);
        Task<List<CorrespondenceNotificationEntity>> GetPrimaryNotificationsByCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken);
    }
}