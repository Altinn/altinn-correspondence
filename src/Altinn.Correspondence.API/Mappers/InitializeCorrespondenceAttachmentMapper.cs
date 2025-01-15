using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceAttachmentMapper
{
    internal static CorrespondenceAttachmentEntity MapToEntity(InitializeCorrespondenceAttachmentExt initializeAttachmentExt, string resourceId, string sender)
    {
        return new CorrespondenceAttachmentEntity
        {
            Created = DateTimeOffset.UtcNow,
            ExpirationTime = initializeAttachmentExt.ExpirationTime,
            Attachment = new AttachmentEntity
            {
                Created = DateTimeOffset.UtcNow,
                FileName = initializeAttachmentExt.FileName,
                Name = initializeAttachmentExt.Name,
                ResourceId = resourceId,
                Sender = sender,
                SendersReference = initializeAttachmentExt.SendersReference,
                Checksum = initializeAttachmentExt.Checksum,
                IsEncrypted = initializeAttachmentExt.IsEncrypted,
                DataLocationType = (AttachmentDataLocationType)initializeAttachmentExt.DataLocationType,
            }
        };
    }
}
