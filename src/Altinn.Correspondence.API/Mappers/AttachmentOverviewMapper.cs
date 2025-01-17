using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Application.MigrateUploadAttachment;

namespace Altinn.Correspondence.Mappers;

internal static class AttachmentOverviewMapper
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
    internal static AttachmentOverviewExt MapToExternal(GetAttachmentOverviewResponse attachmentOverview)
    {
        var fileName = attachmentOverview.FileName ?? string.Empty;
        var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = MimeTypes.ContainsKey(fileExtension) ? MimeTypes[fileExtension] : "application/octet-stream";

        var attachment = new AttachmentOverviewExt
        {
            ResourceId = attachmentOverview.ResourceId,
            AttachmentId = attachmentOverview.AttachmentId,
            Sender = attachmentOverview.Sender,
            Name = attachmentOverview.Name,
            FileName = attachmentOverview.FileName,
            Status = (AttachmentStatusExt)attachmentOverview.Status,
            StatusText = attachmentOverview.StatusText,
            Checksum = attachmentOverview.Checksum,
            StatusChanged = attachmentOverview.StatusChanged,
            DataType = contentType,
            SendersReference = attachmentOverview.SendersReference,
            CorrespondenceIds = attachmentOverview.CorrespondenceIds ?? new List<Guid>(),
        };
        return attachment;
    }
    internal static AttachmentOverviewExt MapMigrateToExternal(MigrateUploadAttachmentResponse overview)
    {
        var attachment = new AttachmentOverviewExt
        {
            ResourceId = overview.ResourceId,
            AttachmentId = overview.AttachmentId,
            Sender = overview.Sender,
            Name = overview.Name,
            FileName = overview.FileName,
            Status = (AttachmentStatusExt)overview.Status,
            StatusText = overview.StatusText,
            Checksum = overview.Checksum,
            StatusChanged = overview.StatusChanged,
            DataType = "lserp",
            SendersReference = overview.SendersReference,
            CorrespondenceIds = overview.CorrespondenceIds ?? new List<Guid>(),
        };
        return attachment;
    }
}
