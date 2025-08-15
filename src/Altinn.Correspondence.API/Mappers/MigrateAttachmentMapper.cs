using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Mappers;

internal static class MigrateAttachmentMapper
{
    internal static MigrateAttachmentRequest MapToRequest(MigrateInitializeAttachmentExt initializeAttachmentExt, HttpRequest httpRequest)
    {
        var attachment = new AttachmentEntity
        {
            ResourceId = initializeAttachmentExt.ResourceId,
            FileName = initializeAttachmentExt.FileName,
            DisplayName = initializeAttachmentExt.DisplayName,
            Sender = initializeAttachmentExt.Sender,
            SendersReference = initializeAttachmentExt.Altinn2SendersReference ?? string.Empty,
            Checksum = initializeAttachmentExt.Checksum,
            IsEncrypted = initializeAttachmentExt.IsEncrypted,
            Created = initializeAttachmentExt.Created,
            Altinn2AttachmentId = initializeAttachmentExt.Altinn2AttachmentId
        };
        return new MigrateAttachmentRequest()
        {
            Attachment = attachment,
            SenderPartyUuid = initializeAttachmentExt.SenderPartyUuid,
            UploadStream = httpRequest.Body,
            ContentLength = httpRequest.ContentLength ?? (httpRequest.Body.CanSeek ? httpRequest.Body.Length : 0)
        };
    }
}
