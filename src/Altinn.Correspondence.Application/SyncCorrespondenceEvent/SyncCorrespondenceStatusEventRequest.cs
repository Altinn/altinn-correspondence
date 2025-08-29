using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceStatusEventRequest
{
    public required Guid CorrespondenceId { get; set; }
    public List<CorrespondenceStatusEntity>? SyncedEvents { get; set; }
    public List<CorrespondenceDeleteEventEntity>? SyncedDeleteEvents { get; set; }
}
