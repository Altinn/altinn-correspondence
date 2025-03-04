using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class OneTimeFixesRepository(ApplicationDbContext context)
    {
        private readonly ApplicationDbContext _context = context;


        public async Task<List<CorrespondenceEntity>> GetCorrespondenceForNameFix(CancellationToken cancellationToken)
        {
            var correspondences = await _context.Correspondences.Where(c => c.MessageSender != null).Include(c => c.ExternalReferences).ToListAsync(cancellationToken);
            return correspondences;
        }
    }
}
