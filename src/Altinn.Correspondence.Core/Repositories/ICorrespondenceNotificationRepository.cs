using Altinn.Correspondence.Core.Models.Entities;
using System.Text.Json;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceNotificationRepository
    {
        Task<Guid> AddNotification(CorrespondenceNotificationEntity notification, CancellationToken cancellationToken);
        Task<CorrespondenceNotificationEntity?> GetPrimaryNotification(Guid correspondenceId, CancellationToken cancellationToken);
        Task WipeOrder(Guid notificationId, CancellationToken cancellationToken);
    }
}