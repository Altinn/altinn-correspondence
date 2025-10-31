using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Application.MigrateUploadAttachment;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Mappers;

internal static class AttachmentOverviewMapper
{
    internal static AttachmentOverviewExt MapToExternal(GetAttachmentOverviewResponse attachmentOverview)
    {
        var fileName = attachmentOverview.FileName;
        var contentType = FileConstants.GetMIMEType(fileName);

        var attachment = new AttachmentOverviewExt
        {
            ResourceId = attachmentOverview.ResourceId,
            AttachmentId = attachmentOverview.AttachmentId,
            Sender = attachmentOverview.Sender,
            FileName = attachmentOverview.FileName,
            DisplayName = attachmentOverview.DisplayName,
            Status = (AttachmentStatusExt)attachmentOverview.Status,
            StatusText = attachmentOverview.StatusText,
            Checksum = attachmentOverview.Checksum,
            StatusChanged = attachmentOverview.StatusChanged,
            DataType = contentType,
            SendersReference = attachmentOverview.SendersReference,
            CorrespondenceIds = attachmentOverview.CorrespondenceIds ?? new List<Guid>(),
            ExpirationTime = attachmentOverview.ExpirationTime,
        };
        return attachment;
    }
    internal static AttachmentOverviewExt MapMigrateToExternal(MigrateAttachmentResponse overview)
    {
        var attachment = new AttachmentOverviewExt
        {
            ResourceId = overview.ResourceId,
            AttachmentId = overview.AttachmentId,
            Sender = overview.Sender,
            FileName = overview.FileName,
            Status = (AttachmentStatusExt)overview.Status,
            StatusText = overview.StatusText,
            Checksum = overview.Checksum,
            StatusChanged = overview.StatusChanged,
            DataType = overview.DataType,
            SendersReference = overview.SendersReference,
            CorrespondenceIds = overview.CorrespondenceIds ?? new List<Guid>(),
        };
        return attachment;
    }
}
