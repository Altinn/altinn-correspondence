using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceNotificationRepository(ApplicationDbContext context) : ICorrespondenceNotificationRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid> AddNotification(CorrespondenceNotificationEntity notification, CancellationToken cancellationToken)
        {
            await _context.CorrespondenceNotifications.AddAsync(notification, cancellationToken);
            await _context.SaveChangesAsync();
            return notification.Id;
        }
    }
}