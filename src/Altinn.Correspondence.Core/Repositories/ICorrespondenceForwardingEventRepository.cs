using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceForwardingEventRepository
    {   
        Task<Guid> AddForwardingEventForSync(CorrespondenceForwardingEventEntity forwardingEvent, CancellationToken cancellationToken);
        Task<List<Guid>> AddForwardingEventsForSync(List<CorrespondenceForwardingEventEntity> forwardingEvents, CancellationToken cancellationToken);
        Task<CorrespondenceForwardingEventEntity> GetForwardingEvent(Guid forwardingEventId, CancellationToken cancellationToken);
        Task SetDialogActivityId(Guid forwardingEventId, Guid dialogActivityId, CancellationToken cancellationToken);
        Task<List<CorrespondenceForwardingEventEntity>> GetForwardingEventsWithoutDialogActivityBatch(int count, DateTimeOffset lastProcessed, CancellationToken cancellationToken);
    }
}