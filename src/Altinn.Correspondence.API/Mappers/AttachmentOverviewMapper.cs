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
            Name = attachmentOverview.Name ?? string.Empty,
            Sender = attachmentOverview.Sender,
            RestrictionName = attachmentOverview.RestrictionName,
            Status = (AttachmentStatusExt)attachmentOverview.Status,
            StatusText = attachmentOverview.StatusText,
            DataLocationUrl = attachmentOverview.DataLocationUrl,
            StatusChanged = attachmentOverview.StatusChanged,
            DataType = attachmentOverview.DataType,
            SendersReference = attachmentOverview.SendersReference,
            CorrespondenceIds = attachmentOverview.CorrespondenceIds
        };
        return attachment;
    }
}
