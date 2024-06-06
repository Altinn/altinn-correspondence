using System.Net.Mail;
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
        public async Task<List<CorrespondenceEntity>> GetNonPublishedCorrespondencesByAttachmentId(Guid attachmentId, AttachmentStatus? attachmentStatus = null, CancellationToken cancellationToken = default)
        {
            var correspondence = await _context.Correspondences.Where(c =>
                !_context.CorrespondenceStatuses.Any(cs => cs.CorrespondenceId == c.Id && (cs.Status >= CorrespondenceStatus.ReadyForPublish)) &&
                c.Content.Attachments.Any(ca => ca.AttachmentId == attachmentId) &&
                    (attachmentStatus == null || c.Content.Attachments.All(ca => ca.Attachment != null && ca.Attachment.Statuses.OrderByDescending(s => s.StatusChanged).First().Status == attachmentStatus))
                ).Include(c => c.Content).ThenInclude(content => content.Attachments).ThenInclude(a => a.Attachment).ToListAsync(cancellationToken);

            return correspondence;
        }
    }
}