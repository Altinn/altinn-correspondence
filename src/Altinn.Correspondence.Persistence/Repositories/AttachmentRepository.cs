using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class AttachmentRepository(ApplicationDbContext context) : IAttachmentRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid> InitializeAttachment(AttachmentEntity attachment, CancellationToken cancellationToken)
        {
            await _context.Attachments.AddAsync(attachment, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return attachment.Id;
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

        // TODO, best practice to pass in ID or whole entity?
        public async Task<bool> SetDataLocationUrl(AttachmentEntity attachmentEntity, AttachmentDataLocationType attachmentDataLocationType, string dataLocationUrl, CancellationToken cancellationToken)
        {
            attachmentEntity.DataLocationType = attachmentDataLocationType;
            attachmentEntity.DataLocationUrl = dataLocationUrl;
            var rowsUpdated = await _context.SaveChangesAsync(cancellationToken);
            return rowsUpdated > 0;
        }
    }
}