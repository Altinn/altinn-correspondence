using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.SyncCorrespondenceStatusEvent;

public class SyncCorrespondenceStatusEventRequest
{
    public required Guid CorrespondenceId { get; set; }
    public CorrespondenceStatusEntity SyncedEvent { get; set; }
}
