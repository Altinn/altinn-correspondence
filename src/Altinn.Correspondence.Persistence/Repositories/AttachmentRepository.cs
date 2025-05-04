using Altinn.Correspondence.Core.Models.Entities;
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
            return await _context.Attachments.SingleOrDefaultAsync(a => a.DataLocationUrl == url, cancellationToken);
        }

        public async Task<AttachmentEntity?> GetAttachmentById(Guid guid, bool includeStatus, CancellationToken cancellationToken)
        {
            var attachments = _context.Attachments.AsQueryable();
            if (includeStatus)
            {
                attachments = attachments.Include(a => a.Statuses);
            }
            return await attachments.SingleOrDefaultAsync(a => a.Id == guid, cancellationToken);
        }

        public async Task<bool> SetDataLocationUrl(AttachmentEntity attachmentEntity, AttachmentDataLocationType attachmentDataLocationType, string dataLocationUrl, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
        {
            attachmentEntity.DataLocationType = attachmentDataLocationType;
            attachmentEntity.DataLocationUrl = dataLocationUrl;
            attachmentEntity.StorageProvider = storageProviderEntity;
            var rowsUpdated = await _context.SaveChangesAsync(cancellationToken);
            return rowsUpdated == 1;

        }

        public async Task<bool> SetChecksum(AttachmentEntity attachmentEntity, string? checkSum, CancellationToken cancellationToken)
        {
            attachmentEntity.Checksum = checkSum;
            var rowsUpdated = await _context.SaveChangesAsync(cancellationToken);
            return rowsUpdated == 1;
        }
        public async Task<bool> SetAttachmentSize(AttachmentEntity attachmentEntity, long size, CancellationToken cancellationToken)
        {
            attachmentEntity.AttachmentSize = size;
            var rowsUpdated = await _context.SaveChangesAsync(cancellationToken);
            return rowsUpdated == 1;
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
        public async Task<AttachmentEntity?> GetAttachmentByCorrespondenceIdAndAttachmentId(Guid correspondenceId, Guid attachmentId, CancellationToken cancellationToken)
        {
            return await _context.Correspondences
                 .Where(c => c.Id == correspondenceId && c.Content!.Attachments.Any(ca => ca.AttachmentId == attachmentId))
                 .Select(c => c.Content!.Attachments.SingleOrDefault(ca => ca.AttachmentId == attachmentId).Attachment).SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<List<AttachmentEntity>> GetAttachmentsByResourceIdWithoutStorageProvider(string resourceId, CancellationToken cancellationToken)
        {
            return await _context.Correspondences
                .Where(c => c.ResourceId == resourceId && c.Content != null)
                .SelectMany(c => c.Content!.Attachments)
                .Where(ca => ca.Attachment.StorageProvider == null)
                .Select(ca => ca.Attachment)
                .ToListAsync(cancellationToken);
        }

        public async Task SetStorageProvider(Guid attachmentId, StorageProviderEntity storageProvider, string dataLocationUrl, CancellationToken cancellationToken)
        {
            var attachment = await _context.Attachments.SingleOrDefaultAsync(a => a.Id == attachmentId);
            if (attachment == null)
            {
                throw new ArgumentException($"Attachment with id {attachmentId} does not exist", nameof(attachmentId));
            }
            attachment.StorageProvider = storageProvider;
            attachment.DataLocationUrl = dataLocationUrl;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}