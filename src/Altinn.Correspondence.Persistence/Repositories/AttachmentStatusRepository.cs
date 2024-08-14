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
            var rowsUpdated = await _context.SaveChangesAsync();
            int expectedChanges = context.ChangeTracker.Entries()
                .Count(e => e.State == EntityState.Added || 
                            e.State == EntityState.Modified || 
                            e.State == EntityState.Deleted);

            if (expectedChanges != rowsUpdated)
            {
                throw new DbUpdateException($"Warning: Expected {expectedChanges} changes in AddAttachmentStatus but {rowsUpdated} changes were made.");
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