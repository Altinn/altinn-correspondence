using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class AttachmentStatusRepository(ApplicationDbContext context) : IAttachmentStatusRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<int> AddAttachmentStatus(AttachmentStatusEntity status, CancellationToken cancellationToken)
        {


            await _context.AttachmentStatuses.AddAsync(status, cancellationToken);
            await _context.SaveChangesAsync();
            return status.Id;
        }
    }
}