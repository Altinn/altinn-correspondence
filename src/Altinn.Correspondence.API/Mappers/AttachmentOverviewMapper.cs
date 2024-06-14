using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class AttachmentOverviewMapper
{
    internal static AttachmentOverviewExt MapToExternal(GetAttachmentOverviewResponse attachmentOverview)
    {
        var attachment = new AttachmentOverviewExt
        {
            AttachmentId = attachmentOverview.AttachmentId,
            Name = attachmentOverview.Name ?? string.Empty,
            Status = (AttachmentStatusExt)attachmentOverview.Status,
            StatusText = attachmentOverview.StatusText,
            StatusChanged = attachmentOverview.StatusChanged,
            DataType = attachmentOverview.DataType,
            IntendedPresentation = (IntendedPresentationTypeExt)attachmentOverview.IntendedPresentation,
            SendersReference = attachmentOverview.SendersReference
        };
        return attachment;
    }
}
