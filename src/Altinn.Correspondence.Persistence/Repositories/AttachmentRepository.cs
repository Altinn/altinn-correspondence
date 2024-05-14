using Altinn.Correspondence.Core.Models;
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
            await _context.SaveChangesAsync();
            return attachment.Id;
        }

        public async Task<List<Guid>> InitializeMultipleAttachments(List<AttachmentEntity> attachments, CancellationToken cancellationToken)
        {
            await _context.Attachments.AddRangeAsync(attachments, cancellationToken);
            await _context.SaveChangesAsync();
            return attachments.Select(a => a.Id).ToList();
        }

        public async Task<AttachmentEntity?> GetAttachmentByUrl(string url, CancellationToken cancellationToken)
        {
            return await _context.Attachments.FirstOrDefaultAsync(a => a.DataLocationUrl == url, cancellationToken);
        }
    }
}