using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Repositories;

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
    }
}