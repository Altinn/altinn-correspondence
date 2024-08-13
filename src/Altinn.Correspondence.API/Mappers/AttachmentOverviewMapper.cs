using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetAttachmentOverview;

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
            RestrictionName = attachmentOverview.RestrictionName,
            Status = (AttachmentStatusExt)attachmentOverview.Status,
            StatusText = attachmentOverview.StatusText,
            Checksum = attachmentOverview.Checksum,
            DataLocationUrl = attachmentOverview.DataLocationUrl,
            StatusChanged = attachmentOverview.StatusChanged,
            DataType = attachmentOverview.DataType,
            SendersReference = attachmentOverview.SendersReference,
            CorrespondenceIds = attachmentOverview.CorrespondenceIds
        };
        return attachment;
    }
}
