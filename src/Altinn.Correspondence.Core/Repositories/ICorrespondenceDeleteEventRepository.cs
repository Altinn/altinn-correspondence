using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceDeleteEventRepository
    {
        Task<CorrespondenceDeleteEventEntity> AddDeleteEvent(CorrespondenceDeleteEventEntity correspondenceForwardingEventEntity, CancellationToken cancellationToken);

        Task<List<CorrespondenceDeleteEventEntity>> GetDeleteEventsForCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken);
    }
}