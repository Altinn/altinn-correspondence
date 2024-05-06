using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IAttachmentStatusRepository
    {
        Task<int> AddAttachmentStatus(AttachmentStatusEntity attachment, CancellationToken cancellationToken);
    }
}