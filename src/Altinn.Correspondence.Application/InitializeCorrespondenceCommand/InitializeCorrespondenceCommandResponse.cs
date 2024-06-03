using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Application.InitializeCorrespondenceCommand;

public class InitializeCorrespondenceCommandResponse
{
    public Guid CorrespondenceId { get; set; }

    public List<Guid> AttachmentIds { get; set; }
}
