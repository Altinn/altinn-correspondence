using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Application.InitializeAttachmentCommand;

public class InitializeAttachmentCommandRequest
{
    public required AttachmentEntity Attachment { get; set; }
}
