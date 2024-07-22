using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceAttachmentRepository(ApplicationDbContext context) : ICorrespondenceAttachmentRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid?> GetAttachmentIdByCorrespondenceAttachmentId(Guid correspondenceAttachmentId, bool onlyPublished, CancellationToken cancellationToken = default)
        {
            if (onlyPublished && !(await _context.Correspondences
                  .AnyAsync(c => c.Content != null && c.Content.Attachments.Any(ca => ca.Id == correspondenceAttachmentId && c.Statuses.Any(s => s.Status == CorrespondenceStatus.Published)
                  && !c.Statuses.Any(s => s.Status == CorrespondenceStatus.PurgedByAltinn || s.Status == CorrespondenceStatus.PurgedByRecipient)
                  ), cancellationToken)))
                return null;

            return await _context.CorrespondenceAttachments
                .Where(ca => ca.Id == correspondenceAttachmentId
                && ((onlyPublished && !ca.Attachment!.Statuses.Any(s => s.Status == AttachmentStatus.Purged)) || !onlyPublished)).Select(ca => ca.AttachmentId).FirstOrDefaultAsync(cancellationToken);
        }
    }
}