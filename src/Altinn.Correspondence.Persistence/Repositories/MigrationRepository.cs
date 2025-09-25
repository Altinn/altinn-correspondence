using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class MigrationRepository(ApplicationDbContext context) : IMigrationRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<CorrespondenceMigrationStatusEntity?> GetCorrespondenceMigrationStatus(Guid correspondenceId, CancellationToken cancellationToken)
        {
            CorrespondenceMigrationStatusEntity? correspondenceMigrationStatus = new CorrespondenceMigrationStatusEntity();

            var correspondence = await _context.Correspondences.FirstOrDefaultAsync(c => c.Id == correspondenceId, cancellationToken);

            correspondenceMigrationStatus.AttachmentStatus.AddRange(from a in correspondence?.Content?.Attachments
                                                                    where a.Attachment?.Statuses.Count > 0
                                                                    select a.Attachment?.Statuses.OrderByDescending(s => s.StatusChanged).First());
            correspondenceMigrationStatus.Status = correspondence?.Statuses.OrderByDescending(s => s.StatusChanged).Last().Status;
            correspondenceMigrationStatus.Altinn2CorrespondenceId = correspondence?.Altinn2CorrespondenceId.GetValueOrDefault();
            correspondenceMigrationStatus.CorrespondenceId = correspondence?.Id;

            return correspondenceMigrationStatus;
        }
    }
}