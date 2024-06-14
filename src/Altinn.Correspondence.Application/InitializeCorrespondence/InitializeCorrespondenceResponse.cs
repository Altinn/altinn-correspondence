using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class InitializeCorrespondenceResponse
{
    public Guid CorrespondenceId { get; set; }

    public List<Guid> AttachmentIds { get; set; } = new List<Guid>();
}
