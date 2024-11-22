using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class InitializeAttachmentRequest
{
    public required AttachmentEntity Attachment { get; set; }
}
