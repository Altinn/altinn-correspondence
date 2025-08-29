using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.LegacyUpdateCorrespondenceStatus;

public class LegacyUpdateCorrespondenceStatusRequest
{
    public required Guid CorrespondenceId { get; set; }
    public required CorrespondenceStatus Status { get; set; }
}
