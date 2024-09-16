using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceNotificationRepository(ApplicationDbContext context) : ICorrespondenceNotificationRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid> AddNotification(CorrespondenceNotificationEntity noticiation, CancellationToken cancellationToken)
        {
            await _context.CorrespondenceNotifications.AddAsync(noticiation, cancellationToken);
            await _context.SaveChangesAsync();
            return noticiation.Id;
        }

    }
}