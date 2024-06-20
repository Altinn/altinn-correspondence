using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceAttachmentRepository(ApplicationDbContext context) : ICorrespondenceAttachmentRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid?> GetAttachmentIdByCorrespondenceAttachmentId(Guid correspondenceAttachmentId, CancellationToken cancellationToken = default)
        {
            return await _context.CorrespondenceAttachments
                .Where(ca => ca.Id == correspondenceAttachmentId).Select(ca => ca.AttachmentId).FirstOrDefaultAsync(cancellationToken);
        }
        public async Task<Guid> PurgeCorrespondenceAttachmentsByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var correspondenceAttachments = await _context.CorrespondenceAttachments
                .Where(ca => ca.AttachmentId == attachmentId)
                .ToListAsync(cancellationToken);

            _context.RemoveRange(correspondenceAttachments);
            await _context.SaveChangesAsync(cancellationToken);

            return attachmentId;
        }
    }
}