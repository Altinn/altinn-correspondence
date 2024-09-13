using Altinn.Correspondence.Core.Models;
namespace Altinn.Correspondece.Application.Helpers;
public static class AttachmentStatusExtensions
{
    public static AttachmentStatusEntity? GetLatestStatus(this AttachmentEntity correspondece)
    {
        var statusEntity = correspondece.Statuses
            .OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        return statusEntity;
    }
}