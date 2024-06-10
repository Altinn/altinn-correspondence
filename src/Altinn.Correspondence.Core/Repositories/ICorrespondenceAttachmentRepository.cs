using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceAttachmentRepository
    {
        Task<Guid> PurgeCorrespondenceAttachmentsByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default);
    }
}