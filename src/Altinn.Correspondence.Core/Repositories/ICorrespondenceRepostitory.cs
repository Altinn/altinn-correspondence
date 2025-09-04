using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceRepository
    {
        Task<CorrespondenceEntity> CreateCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken);

        Task<List<CorrespondenceEntity>> CreateCorrespondences(List<CorrespondenceEntity> correspondences, CancellationToken cancellationToken);

        Task<List<Guid>> GetCorrespondences(
            string resourceId,
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            string orgNo,
            CorrespondencesRoleType role,
            string? sendersReference,
            CancellationToken cancellationToken);

        Task<List<CorrespondenceEntity>> GetCorrespondencesForParties(
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            List<string> recipientIds,
            List<string> resourceIds,
            bool includeActive,
            bool includeArchived,
            string searchString,
            CancellationToken cancellationToken,
            bool filterMigrated = true);


        Task<CorrespondenceEntity?> GetCorrespondenceById(
            Guid guid,
            bool includeStatus,
            bool includeContent,
            bool includeForwardingEvents,
            CancellationToken cancellationToken,
            bool includeIsMigrating = false);

        Task<CorrespondenceEntity> GetCorrespondenceByAltinn2Id(
            int altinn2Id,
            CancellationToken cancellationToken);

        Task<List<CorrespondenceEntity>> GetCorrespondencesByAttachmentId(Guid attachmentId, bool includeStatus, CancellationToken cancellationToken = default);
        Task<List<CorrespondenceEntity>> GetNonPublishedCorrespondencesByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default);
        Task<List<Guid>> GetCorrespondenceIdsByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default);
        Task AddExternalReference(Guid correspondenceId, ReferenceType referenceType, string referenceValue, CancellationToken cancellationToken = default);
        Task UpdatePublished(Guid correspondenceId, DateTimeOffset published, CancellationToken cancellationToken);
        Task UpdateIsMigrating(Guid correspondenceId, bool isMigrating, CancellationToken cancellationToken);
        Task<bool> AreAllAttachmentsPublished(Guid correspondenceId, CancellationToken cancellationToken = default);
        Task<List<CorrespondenceEntity>> GetCandidatesForMigrationToDialogporten(int batchSize, int offset, CancellationToken cancellationToken = default);
        Task<List<CorrespondenceEntity>> GetCorrespondencesWindowAfter(
            int limit,
            DateTimeOffset? lastCreated,
            Guid? lastId,
            bool filterMigrated,
            CancellationToken cancellationToken);

        Task<List<CorrespondenceEntity>> GetCorrespondencesByIdsWithExternalReferenceAndCurrentStatus(
            List<Guid> correspondenceIds,
            ReferenceType referenceType,
            List<CorrespondenceStatus> currentStatuses,
            CancellationToken cancellationToken);
    }
}