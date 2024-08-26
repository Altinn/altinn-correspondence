using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceAttachmentRepository(ApplicationDbContext context) : ICorrespondenceAttachmentRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid?> GetAttachmentIdByCorrespondenceAttachmentId(Guid correspondenceAttachmentId, bool isPublished, CancellationToken cancellationToken = default)
        {
            var correspondenceExists = await _context.Correspondences
                .AnyAsync(c => c.Content != null &&
                    c.Content.Attachments.Any(ca => ca.Id == correspondenceAttachmentId) &&
                    c.Statuses.Any(s => s.Status == CorrespondenceStatus.Published || s.Status == CorrespondenceStatus.Fetched) &&
                    !c.Statuses.Any(s => s.Status == CorrespondenceStatus.PurgedByAltinn || s.Status == CorrespondenceStatus.PurgedByRecipient),
                    cancellationToken);

            if (isPublished && !correspondenceExists)
            {
                return null;
            }


            return await _context.CorrespondenceAttachments
                .Where(ca => ca.Id == correspondenceAttachmentId
                && ((isPublished && !ca.Attachment!.Statuses.Any(s => s.Status == AttachmentStatus.Purged)) || !isPublished)).Select(ca => ca.AttachmentId).SingleOrDefaultAsync(cancellationToken);
        }
    }
}