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
            _context.CorrespondenceNotifications.Add(notification);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return notification.Id;
            }
            catch (DbUpdateException ex) when (ex.IsPostgresUniqueViolation())
            {
                // Just let duplicates fail silently in race conditions, the Empty GUID will be used by the caller to determine that the status was not added, and the log will contain the details of the existing status.
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
                .ThenInclude(c => c.ExternalReferences)
                .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        }

        public async Task<List<CorrespondenceNotificationEntity>> GetNotificationsByIds(List<Guid> notificationIds, CancellationToken cancellationToken)
        {
            return await _context.CorrespondenceNotifications
                .Include(n => n.Correspondence)
                    .ThenInclude(c => c.ExternalReferences)
                .Where(n => notificationIds.Contains(n.Id))
                .ToListAsync(cancellationToken);
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

        public async Task UpdateNotificationStatus(Guid notificationId, string failedStatus, CancellationToken cancellationToken)
        {
            var rows = await _context.CorrespondenceNotifications
            .Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.NotificationOrderStatus, failedStatus),
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

        public async Task<CorrespondencesWithNotificationsBatch> GetCorrespondencesWithSyncedNotifications(
            int count,
            DateTimeOffset lastProcessedTimestamp,
            Guid? lastProcessedId,
            CancellationToken cancellationToken)
        {
            if (count <= 0)
            {
                return new CorrespondencesWithNotificationsBatch();
            }

            var query = _context.CorrespondenceNotifications
                .Where(n => n.Altinn2NotificationId != null 
                         && n.SyncedFromAltinn2 != null);

            // Composite cursor: use both timestamp and Id to prevent skipping at batch boundaries
            if (lastProcessedId.HasValue)
            {
                // Standard case: filter by timestamp OR (same timestamp AND id)
                query = query.Where(n => 
                    n.NotificationSent < lastProcessedTimestamp
                    || (n.NotificationSent == lastProcessedTimestamp && n.Id < lastProcessedId.Value));
            }
            else
            {
                // Initial call: only timestamp filter
                query = query.Where(n => n.NotificationSent < lastProcessedTimestamp);
            }

            // Fetch notifications and group by correspondence in the database
            var notifications = await query
                .OrderByDescending(n => n.NotificationSent)
                .ThenByDescending(n => n.Id)
                .Take(count)
                .Select(n => new { n.Id, n.CorrespondenceId, n.NotificationSent })
                .ToListAsync(cancellationToken);

            if (notifications.Count == 0)
            {
                return new CorrespondencesWithNotificationsBatch();
            }

            // Find the oldest notification for cursor
            var oldestNotification = notifications
                .OrderBy(n => n.NotificationSent)
                .ThenBy(n => n.Id)
                .First();

            // Group by CorrespondenceId
            var grouped = notifications
                .GroupBy(n => n.CorrespondenceId)
                .Select(g => new CorrespondenceWithNotifications
                {
                    CorrespondenceId = g.Key,
                    NotificationIds = g.Select(n => n.Id).ToList()
                })
                .ToList();

            return new CorrespondencesWithNotificationsBatch
            {
                Correspondences = grouped,
                OldestNotificationTimestamp = oldestNotification.NotificationSent,
                OldestNotificationId = oldestNotification.Id,
                TotalNotificationCount = notifications.Count
            };
        }
    }
}