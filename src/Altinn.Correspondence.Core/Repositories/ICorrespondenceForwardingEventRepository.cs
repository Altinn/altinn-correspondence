using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceForwardingEventRepository
    {
        Task<List<CorrespondenceForwardingEventEntity>> AddForwardingEvents(List<CorrespondenceForwardingEventEntity> correspondenceForwardingEventEntities, CancellationToken cancellationToken);
    }
}