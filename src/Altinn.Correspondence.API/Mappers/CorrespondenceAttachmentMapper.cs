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
            AttachmentId = attachment.Id,
            IsEncrypted = attachment.Attachment.IsEncrypted,
            Name = attachment.Name,
            Sender = attachment.Attachment.Sender,
            SendersReference = attachment.Attachment.SendersReference,
            Checksum = attachment.Attachment.Checksum,
            DataLocationType = (AttachmentDataLocationTypeExt)attachment.Attachment.DataLocationType,
            DataLocationUrl = attachment.Attachment.DataLocationUrl,
            RestrictionName = attachment.RestrictionName,
            Status = (AttachmentStatusExt)attachment.Attachment.Statuses.OrderByDescending(s => s.StatusChanged).FirstOrDefault().Status,

        };
        return content;
    }
    internal static List<CorrespondenceAttachmentExt> MapListToExternal(List<CorrespondenceAttachmentEntity> attachments)
    {
        return attachments.Select(MapToExternal).ToList();
    }
}
