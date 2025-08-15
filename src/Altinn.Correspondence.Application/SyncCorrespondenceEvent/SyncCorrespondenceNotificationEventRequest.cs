using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceNotificationEventRequest
{
    public required Guid CorrespondenceId { get; set; }
    public required List<CorrespondenceNotificationEntity> SyncedEvents { get; set; }
}
