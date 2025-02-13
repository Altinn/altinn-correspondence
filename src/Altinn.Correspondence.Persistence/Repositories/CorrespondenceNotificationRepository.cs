using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Azure.Core;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        public async Task<CorrespondenceNotificationEntity?> GetPrimaryNotification(Guid correspondenceId, CancellationToken cancellationToken)
        {
            return await _context.CorrespondenceNotifications
                .Where(n => n.CorrespondenceId == correspondenceId && !n.IsReminder)
                .OrderByDescending(n => n.RequestedSendTime)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task WipeOrder(Guid notificationId, CancellationToken cancellationToken)
        {
            var notification = await _context.CorrespondenceNotifications.FirstOrDefaultAsync(notification => notification.Id == notificationId, cancellationToken);
            if (notification == null)
            {
                throw new ArgumentException($"Notification with id {notificationId} not found");
            }
            notification.OrderRequest = null;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}