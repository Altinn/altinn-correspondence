using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Core.Models;
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
            Sender = initializeAttachmentExt.Sender,
            SendersReference = initializeAttachmentExt.SendersReference,
            RestrictionName = initializeAttachmentExt.RestrictionName,
            DataType = initializeAttachmentExt.DataType,
            Checksum = initializeAttachmentExt.Checksum,
            IsEncrypted = initializeAttachmentExt.IsEncrypted,
            Created = DateTimeOffset.UtcNow,
        };
        return new InitializeAttachmentRequest()
        {
            Attachment = attachment
        };
    }
}
