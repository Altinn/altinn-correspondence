using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class AttachmentStatusRepository(ApplicationDbContext context) : IAttachmentStatusRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid> AddAttachmentStatus(AttachmentStatusEntity status, CancellationToken cancellationToken)
        {
            await _context.AttachmentStatuses.AddAsync(status, cancellationToken);
            await _context.SaveChangesAsync();
            return status.Id;
        }

        public async Task<AttachmentStatusEntity?> GetLatestStatusByAttachmentId(Guid attachmentId, CancellationToken cancellationToken)
        {
            var status = await _context.AttachmentStatuses
                .Where(s => s.AttachmentId == attachmentId)
                .OrderByDescending(s => s.StatusChanged)
                .FirstOrDefaultAsync(cancellationToken);

            return status;
        }
    }
}