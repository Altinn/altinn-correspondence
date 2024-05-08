using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IAttachmentStatusRepository
    {
        Task<Guid> AddAttachmentStatus(AttachmentStatusEntity attachment, CancellationToken cancellationToken);
    }
}