using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models.Entities;


namespace Altinn.Correspondence.Mappers;

internal static class AttachmentStatusMapper
{
    internal static AttachmentStatusEvent MapToExternal(AttachmentStatusEntity AttachmentStatus)
    {
        var attachment = new AttachmentStatusEvent
        {
            Status = (AttachmentStatusExt)AttachmentStatus.Status,
            StatusText = AttachmentStatus.StatusText,
            StatusChanged = AttachmentStatus.StatusChanged
        };
        return attachment;
    }

    internal static List<AttachmentStatusEvent> MapToExternal(List<AttachmentStatusEntity> AttachmentStatuses)
    {
        var attachmentStatuses = new List<AttachmentStatusEvent>();
        foreach (var status in AttachmentStatuses)
        {
            attachmentStatuses.Add(MapToExternal(status));
        }
        return attachmentStatuses;
    }
}
