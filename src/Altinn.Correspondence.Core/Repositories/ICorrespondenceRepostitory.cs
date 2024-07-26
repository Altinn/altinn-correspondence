using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceRepository
    {
        Task<CorrespondenceEntity> InitializeCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken);

        Task<(List<Guid>, int)> GetCorrespondences(
            int offset,
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            CancellationToken cancellationToken);

        Task<CorrespondenceEntity?> GetCorrespondenceById(
            Guid guid,
            bool includeStatus,
            bool includeContent,
            CancellationToken cancellationToken);

        Task<List<CorrespondenceEntity>> GetCorrespondencesByAttachmentId(Guid attachmentId, bool includeStatus, CancellationToken cancellationToken = default);
        Task<List<Guid>> GetNonPublishedCorrespondencesByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default);
        Task<List<Guid>> GetCorrespondenceIdsByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default);
        Task UpdateMarkedUnread(Guid correspondenceId, bool status, CancellationToken cancellationToken);
    }
}