
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface INotificationTemplateRepository
    {
        Task<List<NotificationTemplateEntity>> GetNotificationTemplates(NotificationTemplate template, CancellationToken cancellationToken, string? language = null);
    }
}