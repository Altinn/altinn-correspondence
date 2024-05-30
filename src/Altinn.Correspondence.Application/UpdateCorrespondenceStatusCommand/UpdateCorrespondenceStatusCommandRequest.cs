using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatusCommand;

public class UpdateCorrespondenceStatusCommandRequest
{
    public Guid CorrespondenceId { get; set; }
    public CorrespondenceStatus Status { get; set; }
}
