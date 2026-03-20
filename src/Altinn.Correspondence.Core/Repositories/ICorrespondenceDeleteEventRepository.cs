using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceDeleteEventRepository
    {
        Task<Guid> AddDeleteEventForSync(CorrespondenceDeleteEventEntity correspondenceDeleteEventEntity, CancellationToken cancellationToken);

        Task<List<CorrespondenceDeleteEventEntity>> GetDeleteEventsForCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken);
    }
}