using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceNotificationRepository(ApplicationDbContext context, ILogger<ICorrespondenceNotificationRepository> logger) : ICorrespondenceNotificationRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid> AddNotification(CorrespondenceNotificationEntity notification, CancellationToken cancellationToken)
        {
            await _context.CorrespondenceNotifications.AddAsync(notification, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return notification.Id;
        }

        public async Task<Guid> AddNotificationForSync(CorrespondenceNotificationEntity notification, CancellationToken cancellationToken)
        {
            try
            {
                await _context.CorrespondenceNotifications.AddAsync(notification, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return notification.Id;
            }
            catch (DbUpdateException ex) when (ex.IsPostgresUniqueViolation())
            {
                logger.LogInformation(
                    "Notification event already exists for correspondence {CorrespondenceId} during sync. Altinn2NotificationId: {Altinn2NotificationId}, NotificationSent: {NotificationSent}. Skipping duplicate.",
                    notification.CorrespondenceId,
                    notification.Altinn2NotificationId,
                    notification.NotificationSent);
                
                _context.Entry(notification).State = EntityState.Detached;
                return Guid.Empty;
            }
        }

        public async Task<CorrespondenceNotificationEntity?> GetPrimaryNotification(Guid correspondenceId, CancellationToken cancellationToken)
        {
            return await _context.CorrespondenceNotifications
                .Where(n => n.CorrespondenceId == correspondenceId && !n.IsReminder)
                .OrderByDescending(n => n.RequestedSendTime)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<CorrespondenceNotificationEntity?> GetNotificationById(Guid notificationId, CancellationToken cancellationToken)
        {
            return await _context.CorrespondenceNotifications
                .Include(n => n.Correspondence)
                .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        }

        public async Task UpdateNotificationSent(Guid notificationId, DateTimeOffset sentTime, string destination, CancellationToken cancellationToken)
        {
            var rows = await _context.CorrespondenceNotifications
                .Where(n => n.Id == notificationId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.NotificationSent, sentTime)
                    .SetProperty(n => n.NotificationAddress, destination),
                    cancellationToken);
            if (rows == 0)
                throw new ArgumentException($"Notification with id {notificationId} not found");
        }

        public async Task UpdateOrderResponseData(Guid notificationId, Guid notificationOrderId, Guid shipmentId, CancellationToken cancellationToken)
        {
            var rows = await _context.CorrespondenceNotifications
                .Where(n => n.Id == notificationId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.NotificationOrderId, notificationOrderId)
                    .SetProperty(n => n.ShipmentId, shipmentId),
                    cancellationToken);
            if (rows == 0)
                throw new ArgumentException($"Notification with id {notificationId} not found");
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

        public async Task<List<CorrespondenceNotificationEntity>> GetPrimaryNotificationsByCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken)
        {
            return await _context.CorrespondenceNotifications
                .Where(n => n.CorrespondenceId == correspondenceId && !n.IsReminder)
                .OrderByDescending(n => n.RequestedSendTime)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<NotificationDeliveryRepairCandidate>> GetAltinn3NotificationDeliveryRepairCandidates(
            DateTimeOffset requestedSendTimeOlderThan,
            Guid? afterNotificationId,
            int limit,
            CancellationToken cancellationToken)
        {
            if (limit <= 0)
            {
                return [];
            }

            var query = _context.CorrespondenceNotifications
                .AsNoTracking()
                .Where(cn =>
                    cn.NotificationSent == null &&
                    cn.ShipmentId != null &&
                    cn.Altinn2NotificationId == null &&
                    cn.RequestedSendTime < requestedSendTimeOlderThan)
                .Join(
                    _context.Correspondences.AsNoTracking().Where(c => c.Altinn2CorrespondenceId == null),
                    cn => cn.CorrespondenceId,
                    c => c.Id,
                    (cn, c) => new { NotificationId = cn.Id, CorrespondenceId = c.Id, cn.IsReminder });

            if (afterNotificationId.HasValue)
            {
                var notifId = afterNotificationId.Value;
                query = query.Where(x => x.NotificationId.CompareTo(notifId) > 0);
            }

            return await query
                .OrderBy(x => x.NotificationId)
                .Take(limit)
                .Select(x => new NotificationDeliveryRepairCandidate(x.NotificationId, x.CorrespondenceId, x.IsReminder))
                .ToListAsync(cancellationToken);
        }
    }
}