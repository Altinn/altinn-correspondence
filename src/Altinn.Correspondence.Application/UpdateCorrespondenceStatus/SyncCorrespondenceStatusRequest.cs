using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;

public class SyncCorrespondenceStatusRequest
{
    public required Guid CorrespondenceId { get; set; }
    public required CorrespondenceStatus Status { get; set; }
    public required Guid PartyUuid { get; set; }
}
