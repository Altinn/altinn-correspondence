using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.SyncCorrespondenceStatusEvent;

public class SyncCorrespondenceForwardingEventRequest
{
    public required Guid CorrespondenceId { get; set; }
    public CorrespondenceForwardingEventEntity SyncedEvent { get; set; }
}
