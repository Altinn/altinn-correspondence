using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IAttachmentStatusRepository
    {
        Task<Guid> AddAttachmentStatus(AttachmentStatusEntity attachment, CancellationToken cancellationToken);
    }
}