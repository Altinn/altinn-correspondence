using Altinn.Correspondence.Core.Models.Entities;
namespace Altinn.Correspondence.Application.Helpers;
public static class AttachmentStatusExtensions
{
    public static AttachmentStatusEntity? GetLatestStatus(this AttachmentEntity attachment)
    {
        var statusEntity = attachment.Statuses
            .OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        return statusEntity;
    }
}