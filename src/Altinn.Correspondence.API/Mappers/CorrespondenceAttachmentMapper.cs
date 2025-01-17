using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceAttachmentMapper
{
    private static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>
    {
        { ".pdf", "application/pdf" },
        { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".txt", "text/plain" }
    };

    internal static CorrespondenceAttachmentExt MapToExternal(CorrespondenceAttachmentEntity attachment)
    {
        var fileName = attachment.Attachment.FileName ?? string.Empty;
        var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = MimeTypes.ContainsKey(fileExtension) ? MimeTypes[fileExtension] : "application/octet-stream";

        var content = new CorrespondenceAttachmentExt
        {
            DataType = contentType,
            FileName = attachment.Attachment.FileName,
            Id = attachment.AttachmentId,
            IsEncrypted = attachment.Attachment.IsEncrypted,
            Name = attachment.Attachment.Name,
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
}
