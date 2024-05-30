using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceRepository(ApplicationDbContext context) : ICorrespondenceRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid> InitializeCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
        {
            await _context.Correspondences.AddAsync(correspondence, cancellationToken);
            await _context.SaveChangesAsync();
            return correspondence.Id;
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
                .Where(c => (status == null || (status != null && _context.CorrespondenceStatuses.Where(cs => cs.CorrespondenceId == c.Id).OrderBy(cs => cs.StatusChanged).LastOrDefault().Status == status)) &&
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
            CancellationToken cancellationToken)
        {
            var correspondences = _context.Correspondences.Include(c => c.ReplyOptions).Include(c => c.Notifications).ThenInclude(n => n.Statuses).AsQueryable();
            if (includeStatus)
            {
                correspondences = correspondences.Include(c => c.Statuses);
            }
            return await correspondences.FirstOrDefaultAsync(c => c.Id == guid, cancellationToken);
        }
    }
}