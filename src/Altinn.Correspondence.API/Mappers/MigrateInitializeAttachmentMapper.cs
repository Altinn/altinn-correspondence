using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.MigrateCorrespondenceAttachment;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.API.Mappers;
internal static class MigrateInitializeAttachmentMapper
{
    internal static MigrateInitializeAttachmentRequest MapToRequest(MigrateInitializeAttachmentExt initializeAttachmentExt)
    {
        var attachment = new AttachmentEntity
        {
            ResourceId = initializeAttachmentExt.ResourceId,
            FileName = initializeAttachmentExt.FileName,
            DisplayName = initializeAttachmentExt.DisplayName,
            Sender = initializeAttachmentExt.Sender,
            SendersReference = initializeAttachmentExt.SendersReference,
            Checksum = initializeAttachmentExt.Checksum,
            IsEncrypted = initializeAttachmentExt.IsEncrypted,
            Created = DateTimeOffset.UtcNow,
        };
        return new MigrateInitializeAttachmentRequest()
        {
            Attachment = attachment,
            Altinn2AttachmentId = initializeAttachmentExt.Altinn2AttachmentId
        };
    }
}
