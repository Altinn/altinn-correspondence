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
            var changes = _context.ChangeTracker.DebugView.LongView;
            await _context.AttachmentStatuses.AddAsync(status, cancellationToken);
            var rowsUpdated = await _context.SaveChangesAsync();
            if (rowsUpdated != 1)
            {
                throw new DbUpdateException("Failed to add attachment status: " + changes);
            }
            return status.Id;
        }

        public async Task<AttachmentStatusEntity> GetLatestStatusByAttachmentId(Guid attachmentId, CancellationToken cancellationToken)
        {
            var status = await _context.AttachmentStatuses
                .Where(s => s.AttachmentId == attachmentId)
                .OrderByDescending(s => s.StatusChanged)
                .FirstAsync(cancellationToken);

            return status;
        }
    }
}