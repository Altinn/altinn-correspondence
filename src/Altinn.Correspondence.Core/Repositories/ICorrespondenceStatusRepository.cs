using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceStatusRepository
    {
        Task<Guid> AddCorrespondenceStatus(CorrespondenceStatusEntity correspondenceStatusEntity, CancellationToken cancellationToken);
        Task<Guid> AddCorrespondenceStatusForSync(CorrespondenceStatusEntity correspondenceStatusEntity, CancellationToken cancellationToken);

        Task<Guid> AddCorrespondenceStatusFetched(CorrespondenceStatusFetchedEntity correspondenceStatusFetchedEntity, CancellationToken cancellationToken);

        Task<List<CorrespondenceStatusFetchedEntity>> GetBulkFetchStatusesWindowAfter(int windowSize, DateTimeOffset? afterStatusChanged, Guid? afterId, CancellationToken cancellationToken);

        Task DeleteBulkFetchStatus(Guid id, CancellationToken cancellationToken);
    }
}