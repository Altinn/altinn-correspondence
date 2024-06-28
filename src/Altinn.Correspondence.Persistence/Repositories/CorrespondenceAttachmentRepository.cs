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
            return await _context.CorrespondenceAttachments
                .Where(ca => ca.Id == correspondenceAttachmentId &&
                ((onlyPublished && ca.Attachment!.Statuses.OrderByDescending(s => s.StatusChanged).First().Status == AttachmentStatus.Published) || (!onlyPublished))
                ).Select(ca => ca.AttachmentId).FirstOrDefaultAsync(cancellationToken);
        }
    }
}