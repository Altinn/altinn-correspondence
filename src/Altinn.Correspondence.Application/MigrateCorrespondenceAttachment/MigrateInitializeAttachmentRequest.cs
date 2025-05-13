using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.MigrateCorrespondenceAttachment;

public class MigrateInitializeAttachmentRequest
{
    public required AttachmentEntity Attachment { get; set; }

    public int? Altinn2AttachmentId { get; set; }
}