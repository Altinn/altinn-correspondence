using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class NotificationTemplateRepository(ApplicationDbContext context) : INotificationTemplateRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<List<NotificationTemplateEntity>> GetNotificationTemplates(NotificationTemplate? template, CancellationToken cancellationToken, string? language = null)
        {
            return await _context.NotificationTemplates.Where(a => a.Template == template && (a.Language == null || a.Language.ToLower() == language.ToLower())).ToListAsync(cancellationToken);
        }
    }
}