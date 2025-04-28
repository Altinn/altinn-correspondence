using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class MigrateAttachmentRequest
{
    public required AttachmentEntity Attachment { get; set; }
    public required Guid SenderPartyUuid { get; set; }
    public required Stream UploadStream { get; set; }
    public long ContentLength { get; set; }
}
