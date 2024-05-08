using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IAttachmentRepository
    {
        Task<Guid> InitializeAttachment(AttachmentEntity attachment, CancellationToken cancellationToken);
        Task<List<Guid>> InitializeMultipleAttachments(List<AttachmentEntity> attachments, CancellationToken cancellationToken);
    }
}