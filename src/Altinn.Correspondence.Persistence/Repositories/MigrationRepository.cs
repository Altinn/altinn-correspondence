using System.IO.Compression;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
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
            correspondenceMigrationStatus.AttachmentStatus = correspondence.Content.Attachments.Select(a => a.Attachment.Statuses.OrderByDescending(status => status.StatusChanged).Last()).ToList();
            correspondenceMigrationStatus.Status = correspondence.Statuses.OrderByDescending(s => s.StatusChanged).Last().Status;
            correspondenceMigrationStatus.Altinn2CorrespondenceId = correspondence.Altinn2CorrespondenceId.GetValueOrDefault();
            correspondenceMigrationStatus.CorrespondenceId = correspondence.Id;

            return correspondenceMigrationStatus;
        }
    }
}