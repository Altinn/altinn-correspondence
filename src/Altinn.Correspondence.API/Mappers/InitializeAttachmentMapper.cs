using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeAttachmentMapper
{
    internal static InitializeAttachmentRequest MapToRequest(InitializeAttachmentExt initializeAttachmentExt)
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
            Created = DateTimeOffset.UtcNow
        };
        return new InitializeAttachmentRequest()
        {
            Attachment = attachment
        };
    }
}
