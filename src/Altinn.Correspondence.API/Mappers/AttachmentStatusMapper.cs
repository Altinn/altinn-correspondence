using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models;


namespace Altinn.Correspondence.Mappers;

internal static class AttachmentStatusMapper
{
    internal static AtachmentStatusEvent MapToExternal(AttachmentStatusEntity AttachmentStatus)
    {
        var attachment = new AtachmentStatusEvent
        {
            Status = (AttachmentStatusExt)AttachmentStatus.Status,
            StatusText = AttachmentStatus.StatusText,
            StatusChanged = AttachmentStatus.StatusChanged

        };
        return attachment;
    }

    internal static List<AtachmentStatusEvent> MapToExternal(List<AttachmentStatusEntity> AttachmentStatuses)
    {
        var attachmentStatuses = new List<AtachmentStatusEvent>();
        foreach (var status in AttachmentStatuses)
        {
            attachmentStatuses.Add(MapToExternal(status));
        }
        return attachmentStatuses;
    }
}
