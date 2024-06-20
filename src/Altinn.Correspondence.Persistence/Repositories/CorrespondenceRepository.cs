using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceRepository(ApplicationDbContext context) : ICorrespondenceRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<CorrespondenceEntity> InitializeCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
        {
            await _context.Correspondences.AddAsync(correspondence, cancellationToken);
            await _context.SaveChangesAsync();
            return correspondence;
        }

        public async Task<(List<Guid>, int)> GetCorrespondences(
            int offset,
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            CancellationToken cancellationToken)
        {
            var correspondences = _context.Correspondences
                .Where(c => (status == null || (status != null && _context.CorrespondenceStatuses.Where(cs => cs.CorrespondenceId == c.Id).OrderBy(cs => cs.StatusChanged).Last().Status == status)) &&
                    (from == null || (from != null && c.VisibleFrom > from))
                    && (to == null || (to != null && c.VisibleFrom < to)))
                .OrderByDescending(c => c.VisibleFrom)
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
            var correspondences = _context.Correspondences.Include(c => c.ReplyOptions).Include(c => c.Notifications).ThenInclude(n => n.Statuses).AsQueryable();
            if (includeStatus)
            {
                correspondences = correspondences.Include(c => c.Statuses);
            }
            if (includeContent)
            {
                correspondences = correspondences.Include(c => c.Content).ThenInclude(content => content.Attachments).ThenInclude(a => a.Attachment).ThenInclude(a => a.Statuses);
            }
            return await correspondences.FirstOrDefaultAsync(c => c.Id == guid, cancellationToken);
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
                     && !correspondence.Statuses.Any(status => status.Status == CorrespondenceStatus.Published || cs.Status == CorrespondenceStatus.ReadyForPublish) // Correspondence is not published
                     && correspondence.Content.Attachments.All(correspondenceAttachment => // All attachments of correspondence are published
                            correspondenceAttachment.Attachment.Statuses.Any(statusEntity => statusEntity.Status == AttachmentStatus.Published) // All attachments must be published
                         && !correspondenceAttachment.Attachment.Statuses.Any(statusEntity => statusEntity.Status == AttachmentStatus.Purged))) // No attachments can be purged
                .Select(correspondence => correspondence.Id)
                .ToListAsync(cancellationToken);

            return correspondences;
        }
    }
}