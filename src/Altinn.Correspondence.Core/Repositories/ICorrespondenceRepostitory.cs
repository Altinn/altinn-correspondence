using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceRepository
    {
        Task<CorrespondenceEntity> CreateCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken);

        Task<List<CorrespondenceEntity>> CreateCorrespondences(List<CorrespondenceEntity> correspondences, CancellationToken cancellationToken);

        Task<(List<Guid>, int)> GetCorrespondences(
            string resourceId,
            int offset,
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            string orgNo,
            CorrespondencesRoleType role,
            CancellationToken cancellationToken);

        Task<(List<CorrespondenceEntity>, int)> GetCorrespondencesForParties(
            int offset,
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            List<string> recipientIds,
            List<string> resourceIds,
            string language,
            bool includeActive,
            bool includeArchived,
            bool includePurged,
            string searchString,
            CancellationToken cancellationToken);


        Task<CorrespondenceEntity?> GetCorrespondenceById(
            Guid guid,
            bool includeStatus,
            bool includeContent,
            CancellationToken cancellationToken);

        Task<List<CorrespondenceEntity>> GetCorrespondencesByAttachmentId(Guid attachmentId, bool includeStatus, CancellationToken cancellationToken = default);
        Task<List<Guid>> GetNonPublishedCorrespondencesByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default);
        Task<List<Guid>> GetCorrespondenceIdsByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default);
        Task AddExternalReference(Guid correspondenceId, ReferenceType referenceType, string referenceValue, CancellationToken cancellationToken = default);
        Task UpdateMarkedUnread(Guid correspondenceId, bool status, CancellationToken cancellationToken);
        Task UpdatePublished(Guid correspondenceId, DateTimeOffset published, CancellationToken cancellationToken);
    }
}