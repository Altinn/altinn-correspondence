using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Application.InitializeCorrespondenceCommand;

public class InitializeCorrespondenceCommandRequest
{
    public CorrespondenceEntity correspondence { get; set; }
    public List<AttachmentEntity> newAttachments { get; set; }
    public List<Guid> existingAttachments { get; set; }
}
