using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceDeleteEventRepository
    {
        Task<List<CorrespondenceDeleteEventEntity>> AddDeleteEvents(List<CorrespondenceDeleteEventEntity> correspondenceForwardingEventEntities, CancellationToken cancellationToken);

        Task<List<CorrespondenceDeleteEventEntity>> GetDeleteEventsForCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken);
    }
}