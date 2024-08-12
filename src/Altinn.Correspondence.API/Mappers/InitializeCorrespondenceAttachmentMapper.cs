using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceAttachmentMapper
{
    internal static List<CorrespondenceAttachmentEntity> MapListToEntities(List<InitializeCorrespondenceAttachmentExt> initializeAttachmentExt, string resourceId)
    {
        var attachments = new List<CorrespondenceAttachmentEntity>();
        foreach (var attachment in initializeAttachmentExt)
        {
            attachments.Add(MapToEntity(attachment, resourceId));
        }
        return attachments;
    }
    internal static CorrespondenceAttachmentEntity MapToEntity(InitializeCorrespondenceAttachmentExt initializeAttachmentExt, string resourceId)
    {
        return new CorrespondenceAttachmentEntity
        {
            Name = initializeAttachmentExt.Name,
            RestrictionName = initializeAttachmentExt.RestrictionName,
            ExpirationTime = initializeAttachmentExt.ExpirationTime,
            Attachment = new AttachmentEntity
            {
                Created = DateTimeOffset.UtcNow,
                FileName = initializeAttachmentExt.FileName,
                ResourceId = resourceId,
                Sender = initializeAttachmentExt.Sender,
                SendersReference = initializeAttachmentExt.SendersReference,
                DataType = initializeAttachmentExt.DataType,
                Checksum = initializeAttachmentExt.Checksum,
                IsEncrypted = initializeAttachmentExt.IsEncrypted,
                DataLocationUrl = initializeAttachmentExt.DataLocationUrl,
                DataLocationType = (AttachmentDataLocationType)initializeAttachmentExt.DataLocationType,
            }
        };
    }
}
