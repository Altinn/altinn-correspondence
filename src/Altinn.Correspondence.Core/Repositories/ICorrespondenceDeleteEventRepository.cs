using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceDeleteEventRepository
    {
        Task<CorrespondenceDeleteEventEntity> AddDeleteEvent(CorrespondenceDeleteEventEntity correspondenceDeleteEventEntity, CancellationToken cancellationToken);

        Task<List<CorrespondenceDeleteEventEntity>> GetDeleteEventsForCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken);

        Task<Dictionary<Guid, bool>> GetSoftDeleteStates(IReadOnlyCollection<Guid> correspondenceIds, CancellationToken cancellationToken);
    }
}