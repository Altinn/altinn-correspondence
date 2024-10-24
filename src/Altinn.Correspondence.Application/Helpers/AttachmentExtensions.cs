using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
namespace Altinn.Correspondence.Application.Helpers;
public static class AttachmentStatusExtensions
{
    public static AttachmentStatusEntity? GetLatestStatus(this AttachmentEntity attachment)
    {
        var statusEntity = attachment.Statuses
            .OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        return statusEntity;
    }
    public static bool StatusHasBeen(this AttachmentEntity attachment, AttachmentStatus status)
    {
        return attachment.Statuses.Any(s => s.Status == status);
    }
}