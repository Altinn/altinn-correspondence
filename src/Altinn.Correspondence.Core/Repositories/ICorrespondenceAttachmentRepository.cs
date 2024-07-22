using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceAttachmentRepository
    {
        Task<Guid?> GetAttachmentIdByCorrespondenceAttachmentId(Guid correspondenceAttachmentId, bool isPublished, CancellationToken cancellationToken);
    }
}