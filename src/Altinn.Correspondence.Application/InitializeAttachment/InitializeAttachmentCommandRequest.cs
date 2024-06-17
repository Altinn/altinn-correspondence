using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class InitializeAttachmentRequest
{
    public required AttachmentEntity Attachment { get; set; }
}
