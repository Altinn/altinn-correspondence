using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Mappers;

internal static class MigrateAttachmentMapper
{
    internal static async Task<MigrateAttachmentRequest> MapToRequestAsync(MigrateInitializeAttachmentExt initializeAttachmentExt, HttpRequest httpRequest, ServiceOwnerHelper serviceOwnerHelper, CancellationToken cancellationToken)
    {
        var attachment = new AttachmentEntity
        {
            ResourceId = initializeAttachmentExt.ResourceId,
            FileName = initializeAttachmentExt.FileName,
            DisplayName = initializeAttachmentExt.DisplayName,
            Sender = initializeAttachmentExt.Sender,
            ServiceOwnerId = await serviceOwnerHelper.GetSafeServiceOwnerIdAsync(initializeAttachmentExt.Sender, cancellationToken),
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
