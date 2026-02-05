using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceAttachmentMapper
{

    internal static CorrespondenceAttachmentExt MapToExternal(CorrespondenceAttachmentEntity attachment)
    {
        var fileName = attachment.Attachment.FileName;
        var contentType = FileConstants.GetMIMEType(fileName);

        var content = new CorrespondenceAttachmentExt
        {
            DataType = contentType,
            FileName = attachment.Attachment.FileName,
            DisplayName = attachment.Attachment.DisplayName,
            Id = attachment.AttachmentId,
            IsEncrypted = attachment.Attachment.IsEncrypted,
            SendersReference = attachment.Attachment.SendersReference,
            Checksum = attachment.Attachment.Checksum,
            DataLocationType = (AttachmentDataLocationTypeExt)attachment.Attachment.DataLocationType,
            Status = (AttachmentStatusExt)attachment.Attachment.GetLatestStatus()!.Status,
            StatusText = attachment.Attachment.GetLatestStatus()!.StatusText,
            StatusChanged = attachment.Attachment.GetLatestStatus()!.StatusChanged,
            Created = attachment.Created,
            ExpirationTime = attachment.ExpirationTime
        };
        return content;
    }
    internal static LegacyCorrespondenceAttachmentExt MapToExternalLegacy(CorrespondenceAttachmentEntity attachment)
    {
        var fileName = attachment.Attachment.FileName;
        var contentType = FileConstants.GetMIMEType(fileName);

        var content = new LegacyCorrespondenceAttachmentExt
        {
            DataType = contentType,
            FileName = attachment.Attachment.FileName,
            Name = attachment.Attachment.DisplayName ?? attachment.Attachment.FileName,
            Id = attachment.AttachmentId,
            IsEncrypted = attachment.Attachment.IsEncrypted,
            SendersReference = attachment.Attachment.SendersReference,
            Checksum = attachment.Attachment.Checksum,
            DataLocationType = (AttachmentDataLocationTypeExt)attachment.Attachment.DataLocationType,
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
    internal static List<LegacyCorrespondenceAttachmentExt> MapListToExternalLegacy(List<CorrespondenceAttachmentEntity> attachments)
    {
        return attachments.Select(MapToExternalLegacy).ToList();
    }
}
