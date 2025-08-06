using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.SyncCorrespondenceStatusEvent;

public class SyncCorrespondenceNotificationEventRequest
{
    public required Guid CorrespondenceId { get; set; }
    public CorrespondenceNotificationEntity SyncedEvent { get; set; }
}
