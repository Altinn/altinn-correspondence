using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Application.MigrateUploadAttachment;

namespace Altinn.Correspondence.Mappers;

internal static class AttachmentOverviewMapper
{
    internal static AttachmentOverviewExt MapToExternal(GetAttachmentOverviewResponse attachmentOverview)
    {
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
            DataType = attachmentOverview.DataType,
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
            DataType = overview.DataType,
            SendersReference = overview.SendersReference,
            CorrespondenceIds = overview.CorrespondenceIds ?? new List<Guid>(),
        };
        return attachment;
    }
}
