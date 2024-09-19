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
            Name = initializeAttachmentExt.Name,
            RestrictionName = initializeAttachmentExt.RestrictionName,
            Sender = initializeAttachmentExt.Sender,
            SendersReference = initializeAttachmentExt.SendersReference,
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
