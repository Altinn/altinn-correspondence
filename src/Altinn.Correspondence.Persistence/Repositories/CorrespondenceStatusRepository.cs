using Altinn.Correspondence.Core.Models.Entities;
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

        public async Task<List<Guid>> AddCorrespondenceStatuses(List<CorrespondenceStatusEntity> statuses, CancellationToken cancellationToken)
        {
            await _context.CorrespondenceStatuses.AddRangeAsync(statuses, cancellationToken);
            await _context.SaveChangesAsync();
            return statuses.Select(s => s.Id).ToList();
        }

        public async Task<CorrespondenceStatusEntity> GetLatestStatusByCorrespondenceId(Guid CorrespondenceId, CancellationToken cancellationToken)
        {
            var status = await _context.CorrespondenceStatuses
                .Where(s => s.CorrespondenceId == CorrespondenceId)
                .OrderByDescending(s => s.StatusChanged)
                .FirstAsync(cancellationToken);

            return status;
        }
    }
}