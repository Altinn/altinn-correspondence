using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceNotificationRepository
    {
        Task<Guid> AddNotification(CorrespondenceNotificationEntity notification, CancellationToken cancellationToken);


    }
}