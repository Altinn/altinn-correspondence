using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class AttachmentRepository(ApplicationDbContext context) : IAttachmentRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<int> InitializeAttachment(AttachmentEntity attachment, CancellationToken cancellationToken)
        {
            await _context.Attachments.AddAsync(attachment, cancellationToken);
            await _context.SaveChangesAsync();
            return attachment.Id;
        }
    }
}