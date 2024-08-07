using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IAttachmentRepository
    {
        Task<AttachmentEntity> InitializeAttachment(AttachmentEntity attachment, CancellationToken cancellationToken);
        Task<List<Guid>> InitializeMultipleAttachments(List<AttachmentEntity> attachments, CancellationToken cancellationToken);
        Task<AttachmentEntity?> GetAttachmentByUrl(string url, CancellationToken cancellationToken);
        Task<AttachmentEntity?> GetAttachmentById(Guid attachmentId, bool includeStatus = false, CancellationToken cancellationToken = default);
        Task<bool> SetDataLocationUrl(AttachmentEntity attachmentEntity, AttachmentDataLocationType attachmentDataLocationType, string dataLocationUrl, CancellationToken cancellationToken);
        Task<bool> SetChecksum(AttachmentEntity attachmentEntity, string? checksum, CancellationToken cancellationToken);
        Task<bool> CanAttachmentBeDeleted(Guid attachmentId, CancellationToken cancellationToken);
        Task<List<AttachmentEntity>> GetAttachmentsByCorrespondence(Guid correspondenceId, CancellationToken cancellationToken);
    }
}
