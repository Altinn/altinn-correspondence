using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceStatusRepository(ApplicationDbContext context) : ICorrespondenceStatusRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid> AddCorrespondenceStatus(CorrespondenceStatusEntity status, CancellationToken cancellationToken)
        {
            await _context.CorrespondenceStatuses.AddAsync(status, cancellationToken);
            await _context.SaveChangesAsync();
            return status.Id;
        }

        public async Task<CorrespondenceStatusEntity?> GetLatestStatusByCorrespondenceId(Guid CorrespondenceId, CancellationToken cancellationToken)
        {
            var status = await _context.CorrespondenceStatuses
                .Where(s => s.CorrespondenceId == CorrespondenceId)
                .OrderByDescending(s => s.StatusChanged)
                .FirstOrDefaultAsync(cancellationToken);

            return status;
        }
    }
}