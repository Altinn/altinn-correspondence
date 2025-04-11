using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class MigrateAttachmentRequest
{
    public required AttachmentEntity Attachment { get; set; }
    public required Guid SenderPartyUuid { get; set; }
}
