using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class AttachmentRepository(ApplicationDbContext context, ILogger<IAttachmentRepository> logger) : IAttachmentRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<AttachmentEntity> InitializeAttachment(AttachmentEntity attachment, CancellationToken cancellationToken)
        {
            if (attachment.StorageProvider?.Id is not null)
            {
                attachment.StorageProvider = await _context.StorageProviders
                    .FirstOrDefaultAsync(sp => sp.Id == attachment.StorageProvider.Id, cancellationToken);
            }
            else
            {
                logger.LogWarning("Could not find any storage provider for attachment");
            }

            await _context.Attachments.AddAsync(attachment, cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error saving attachment {AttachmentId} (ResourceId: {ResourceId}, StorageProviderId: {StorageProviderId})",
                    attachment?.Id, attachment?.ResourceId, attachment?.StorageProvider?.Id);

                throw;
            }

            return attachment;
        }

        public async Task<List<Guid>> InitializeMultipleAttachments(List<AttachmentEntity> attachments, CancellationToken cancellationToken)
        {
            await _context.Attachments.AddRangeAsync(attachments, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return attachments.Select(a => a.Id).ToList();
        }

        public async Task<AttachmentEntity> GetAttachmentByAltinn2Id(string altinn2Id, CancellationToken cancellationToken)
        {
            return await _context.Attachments.SingleAsync(a => a.Altinn2AttachmentId == altinn2Id, cancellationToken);
        }

        public async Task<AttachmentEntity?> GetAttachmentById(Guid guid, bool includeStatus, CancellationToken cancellationToken)
        {
            logger.LogDebug("Retrieving attachment {AttachmentId} with status included: {IncludeStatus}", guid, includeStatus);
            var attachments = _context.Attachments.AsQueryable();
            if (includeStatus)
            {
                attachments = attachments.Include(a => a.Statuses);
            }
            attachments = attachments.Include(a => a.StorageProvider);
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
            return await _context.Correspondences
                .Where(c => c.Id == correspondenceId && c.Content != null)
                .SelectMany(c => c.Content!.Attachments)
                .Include(ca => ca.Attachment)
                .ThenInclude(a => a!.StorageProvider)
                .Select(ca => ca.Attachment)
                .ToListAsync(cancellationToken);
        }

        public async Task<AttachmentEntity?> GetAttachmentByCorrespondenceIdAndAttachmentId(Guid correspondenceId, Guid attachmentId, CancellationToken cancellationToken)
        {
            return await _context.Correspondences
                .Where(c => c.Id == correspondenceId && c.Content!.Attachments.Any(ca => ca.AttachmentId == attachmentId))
                .SelectMany(c => c.Content!.Attachments)
                .Include(ca => ca.Attachment)
                .ThenInclude(a => a!.StorageProvider)
                .Where(ca => ca.AttachmentId == attachmentId)
                .Select(ca => ca.Attachment)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<List<AttachmentEntity>> GetAttachmentsByResourceIdWithoutStorageProvider(string resourceId, CancellationToken cancellationToken)
        {
            return await _context.Attachments
                .Where(a => a.ResourceId == resourceId && a.StorageProvider == null)
                .Include(a => a.StorageProvider)
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

		public async Task<int> HardDeleteOrphanedAttachments(List<Guid> attachmentIds, CancellationToken cancellationToken)
		{
			var orphanAttachments = await _context.Attachments
				.Where(a => attachmentIds.Contains(a.Id))
				.Where(a => !_context.CorrespondenceAttachments.Any(ca => ca.AttachmentId == a.Id))
				.ToListAsync(cancellationToken);

			if (orphanAttachments.Count == 0)
			{
				return 0;
			}

			_context.Attachments.RemoveRange(orphanAttachments);
			return await _context.SaveChangesAsync(cancellationToken);
		}

        public async Task<List<Guid>> GetAttachmentIdsOnResource(string resourceId, CancellationToken cancellationToken)
        {
            return await _context.Attachments
                .Where(a => a.ResourceId == resourceId)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);
        }
	}
}