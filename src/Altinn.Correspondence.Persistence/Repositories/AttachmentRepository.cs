using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class AttachmentRepository(ApplicationDbContext context) : IAttachmentRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<AttachmentEntity> InitializeAttachment(AttachmentEntity attachment, CancellationToken cancellationToken)
        {

            await _context.Attachments.AddAsync(attachment, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return attachment;
        }

        public async Task<List<Guid>> InitializeMultipleAttachments(List<AttachmentEntity> attachments, CancellationToken cancellationToken)
        {
            await _context.Attachments.AddRangeAsync(attachments, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return attachments.Select(a => a.Id).ToList();
        }

        public async Task<AttachmentEntity?> GetAttachmentByUrl(string url, CancellationToken cancellationToken)
        {
            return await _context.Attachments.FirstOrDefaultAsync(a => a.DataLocationUrl == url, cancellationToken);
        }

        public async Task<AttachmentEntity?> GetAttachmentById(Guid guid, bool includeStatus, CancellationToken cancellationToken)
        {
            var attachments = _context.Attachments.AsQueryable();
            if (includeStatus)
            {
                attachments = attachments.Include(a => a.Statuses);
            }
            return await attachments.FirstOrDefaultAsync(a => a.Id == guid, cancellationToken);
        }

        public async Task<string> SetDataLocationUrl(AttachmentEntity attachmentEntity, AttachmentDataLocationType attachmentDataLocationType, string dataLocationUrl, CancellationToken cancellationToken)
        {
            attachmentEntity.DataLocationType = attachmentDataLocationType;
            attachmentEntity.DataLocationUrl = dataLocationUrl;
            string changes = _context.ChangeTracker.DebugView.LongView;
            var rowsUpdated = await _context.SaveChangesAsync(cancellationToken);
            if (rowsUpdated != 1)
            {
                return changes;
            }
            return string.Empty;
        }

        public async Task<string> SetChecksum(AttachmentEntity attachmentEntity, string? checkSum, CancellationToken cancellationToken)
        {
            attachmentEntity.Checksum = checkSum;
            string changes = _context.ChangeTracker.DebugView.LongView;
            var rowsUpdated = await _context.SaveChangesAsync(cancellationToken);
            if (rowsUpdated != 1)
            {
                return changes;
            }
            return string.Empty;
        }

        public async Task<bool> CanAttachmentBeDeleted(Guid attachmentId, CancellationToken cancellationToken)
        {
            return !(await _context.Correspondences.AnyAsync(a => a.Content != null && a.Content.Attachments.Any(ca => ca.AttachmentId == attachmentId) &&
            !a.Statuses.Any(s => s.Status == CorrespondenceStatus.PurgedByRecipient || s.Status == CorrespondenceStatus.PurgedByAltinn), cancellationToken));
        }
        public async Task<List<AttachmentEntity?>> GetAttachmentsByCorrespondence(Guid correspondenceId, CancellationToken cancellationToken)
        {
            return await _context.Correspondences.Where(c => c.Id == correspondenceId && c.Content != null).SelectMany(c => c.Content!.Attachments).Select(ca => ca.Attachment).ToListAsync(cancellationToken);
        }
    }
}