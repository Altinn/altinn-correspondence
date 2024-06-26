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
            DataType = attachment.DataType,
            FileName = attachment.Attachment.FileName,
            AttachmentId = attachment.Id,
            IsEncrypted = attachment.IsEncrypted,
            Name = attachment.Name,
            SendersReference = attachment.SendersReference,
            Checksum = attachment.Checksum,
            DataLocationType = (AttachmentDataLocationTypeExt)attachment.DataLocationType,
            DataLocationUrl = attachment.DataLocationUrl,
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
