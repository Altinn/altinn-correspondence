using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceForwardingEventRequest
{
    public required Guid CorrespondenceId { get; set; }
    public required List<CorrespondenceForwardingEventEntity> SyncedEvents { get; set; }
}
