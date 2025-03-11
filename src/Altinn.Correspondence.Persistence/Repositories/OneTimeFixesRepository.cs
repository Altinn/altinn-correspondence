using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class OneTimeFixesRepository(ApplicationDbContext context) : IOneTimeFixesRepository
    {
        private readonly ApplicationDbContext _context = context;


        public async Task<List<CorrespondenceEntity>> GetCorrespondenceForNameFix(CancellationToken cancellationToken)
        {
            var correspondences = await _context.Correspondences.Where(c => c.MessageSender != null).Include(c => c.ExternalReferences).ToListAsync(cancellationToken);
            return correspondences;
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesWithoutConfirmation(CancellationToken cancellationToken)
        {
            var correspondences = await _context.Correspondences.Where(c => !c.IsConfirmationNeeded).Include(c => c.ExternalReferences).ToListAsync(cancellationToken);
            return correspondences;
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondences(CancellationToken cancellationToken)
        {
            var correspondences = await _context.Correspondences.Include(c => c.ExternalReferences).ToListAsync(cancellationToken);
            return correspondences;
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesWithoutOpenedStatus(CancellationToken cancellationToken)
        {
            var timeFixForNewWasCreated = DateTime.Now; // TODO, set date filter, also, consider using DialogId instead as it is more accurate to dialog creation time and should be sortable
            return await _context.Correspondences.Where(c => c.Created < DateTime.Now).Include(c => c.ExternalReferences).ToListAsync(cancellationToken);
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesWithArchivedAction(CancellationToken cancellationToken)
        {
            var timeFixForNewWasCreated = DateTime.Now; // TODO, set date filter, also, consider using DialogId instead as it is more accurate to dialog creation time and should be sortable
            return await _context.Correspondences.Where(c => c.Created < DateTime.Now).Include(c => c.ExternalReferences).ToListAsync(cancellationToken);
        }
    }
}
