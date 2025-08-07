using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceStatusEventRequest
{
    public required Guid CorrespondenceId { get; set; }
    public required List<CorrespondenceStatusEntity> SyncedEvents { get; set; }
}
