using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceForwardingEventRepository
    {
        Task<Guid> AddForwardingEvent(CorrespondenceForwardingEventEntity forwardingEvent, CancellationToken cancellationToken);
        Task<List<CorrespondenceForwardingEventEntity>> GetForwardingEvents(Guid correspondenceId, CancellationToken cancellationToken);
    }
}