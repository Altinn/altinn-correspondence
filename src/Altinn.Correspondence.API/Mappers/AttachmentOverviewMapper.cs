using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetAttachmentOverview;
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
            ExpirationInDays = attachmentOverview.ExpirationInDays,
        };
        return attachment;
    }
    
}
