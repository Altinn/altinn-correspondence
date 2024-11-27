using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;

public class UpdateCorrespondenceStatusRequest
{
    public required Guid CorrespondenceId { get; set; }
    public required CorrespondenceStatus Status { get; set; }
}
