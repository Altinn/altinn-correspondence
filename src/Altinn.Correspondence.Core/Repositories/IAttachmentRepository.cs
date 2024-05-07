using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IAttachmentRepository
    {
        Task<int> InitializeAttachment(AttachmentEntity attachment, CancellationToken cancellationToken);
    }
}