using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceRepository(ApplicationDbContext context) : ICorrespondenceRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<CorrespondenceEntity> CreateCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
        {
            await _context.Correspondences.AddAsync(correspondence, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return correspondence;
        }
        public async Task<List<CorrespondenceEntity>> CreateCorrespondences(List<CorrespondenceEntity> correspondences, CancellationToken cancellationToken)
        {
            await _context.Correspondences.AddRangeAsync(correspondences, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return correspondences;
        }

        public async Task<(List<Guid>, int)> GetCorrespondences(
            string resourceId,
            int offset,
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            CancellationToken cancellationToken)
        {
            var correspondences = _context.Correspondences
                .Where(c => c.ResourceId == resourceId)             // Correct id
                .Where(c => from == null || c.VisibleFrom > from)   // From date filter
                .Where(c => to == null || c.VisibleFrom < to)       // To date filter
                .OrderByDescending(c => c.VisibleFrom)              // Sort by visibleFrom
                .WithValidStatuses(status)                          // Filter by status
                .Select(c => c.Id);

            var totalItems = await correspondences.CountAsync(cancellationToken);
            var result = await correspondences.Skip(offset).Take(limit).ToListAsync(cancellationToken);
            return (result, totalItems);
        }

        public async Task<CorrespondenceEntity?> GetCorrespondenceById(
            Guid guid,
            bool includeStatus,
            bool includeContent,
            CancellationToken cancellationToken)
        {
            var correspondences = _context.Correspondences.Include(c => c.ReplyOptions).Include(c => c.ExternalReferences).Include(c => c.Notifications).AsQueryable();
            if (includeStatus)
            {
                correspondences = correspondences.Include(c => c.Statuses);
            }
            if (includeContent)
            {
                correspondences = correspondences.Include(c => c.Content).ThenInclude(content => content.Attachments).ThenInclude(a => a.Attachment).ThenInclude(a => a.Statuses);
            }
            return await correspondences.SingleOrDefaultAsync(c => c.Id == guid, cancellationToken);
        }
        public async Task<List<CorrespondenceEntity>> GetCorrespondencesByAttachmentId(Guid attachmentId, bool includeStatus, CancellationToken cancellationToken = default)
        {
            var correspondence = _context.Correspondences.
                Where(c => c.Content != null && c.Content.Attachments.Any(ca => ca.AttachmentId == attachmentId)).AsQueryable();

            correspondence = includeStatus ? correspondence.Include(c => c.Statuses) : correspondence;
            return await correspondence.ToListAsync(cancellationToken);
        }

        public async Task<List<Guid>> GetNonPublishedCorrespondencesByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var correspondences = await _context.Correspondences
                .Where(correspondence =>
                        correspondence.Content!.Attachments.Any(attachment => attachment.AttachmentId == attachmentId) // Correspondence has the given attachment
                     && !correspondence.Statuses.Any(status => status.Status == CorrespondenceStatus.Published || status.Status == CorrespondenceStatus.ReadyForPublish) // Correspondence is not published
                     && correspondence.Content.Attachments.All(correspondenceAttachment => // All attachments of correspondence are published
                            correspondenceAttachment.Attachment.Statuses.Any(statusEntity => statusEntity.Status == AttachmentStatus.Published) // All attachments must be published
                         && !correspondenceAttachment.Attachment.Statuses.Any(statusEntity => statusEntity.Status == AttachmentStatus.Purged))) // No attachments can be purged
                .Select(correspondence => correspondence.Id)
                .ToListAsync(cancellationToken);

            return correspondences;
        }

        public async Task<List<Guid>> GetCorrespondenceIdsByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var correspondenceIds = await _context.Correspondences
            .Where(c => c.Content != null && c.Content.Attachments.Any(ca => ca.AttachmentId == attachmentId))
            .Select(c => c.Id).ToListAsync(cancellationToken);
            return correspondenceIds;
        }
        public async Task UpdateMarkedUnread(Guid correspondenceId, bool status, CancellationToken cancellationToken)
        {
            var correspondence = await _context.Correspondences.SingleOrDefaultAsync(c => c.Id == correspondenceId, cancellationToken);
            if (correspondence != null)
            {
                correspondence.MarkedUnread = status;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}