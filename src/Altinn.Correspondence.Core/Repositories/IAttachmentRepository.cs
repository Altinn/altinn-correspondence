using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IAttachmentRepository
    {
        Task<Guid> InitializeAttachment(AttachmentEntity attachment, CancellationToken cancellationToken);
        Task<List<Guid>> InitializeMultipleAttachments(List<AttachmentEntity> attachments, CancellationToken cancellationToken);
        Task<AttachmentEntity?> GetAttachmentByUrl(string url, CancellationToken cancellationToken);
        Task<AttachmentEntity?> GetAttachmentById(Guid attachmentId, bool includeStatus = false, CancellationToken cancellationToken = default);
    }
}