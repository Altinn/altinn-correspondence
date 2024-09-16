using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceAttachmentMapper
{
    internal static CorrespondenceAttachmentExt MapToExternal(CorrespondenceAttachmentEntity attachment)
    {
        var content = new CorrespondenceAttachmentExt
        {
            DataType = attachment.Attachment.DataType,
            FileName = attachment.Attachment.FileName,
            Id = attachment.AttachmentId,
            IsEncrypted = attachment.Attachment.IsEncrypted,
            Name = attachment.Attachment.Name,
            SendersReference = attachment.Attachment.SendersReference,
            Checksum = attachment.Attachment.Checksum,
            DataLocationType = (AttachmentDataLocationTypeExt)attachment.Attachment.DataLocationType,
            RestrictionName = attachment.Attachment.RestrictionName,
            Status = (AttachmentStatusExt)attachment.Attachment.GetLatestStatus()!.Status,
            StatusText = attachment.Attachment.GetLatestStatus()!.StatusText,
            StatusChanged = attachment.Attachment.GetLatestStatus()!.StatusChanged,
            Created = attachment.Created,
            ExpirationTime = attachment.ExpirationTime
        };
        return content;
    }
    internal static List<CorrespondenceAttachmentExt> MapListToExternal(List<CorrespondenceAttachmentEntity> attachments)
    {
        return attachments.Select(MapToExternal).ToList();
    }
}
