using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
namespace Altinn.Correspondece.Application.Helpers;
public static class CorrespondenceStatusExtensions
{
    public static CorrespondenceStatusEntity? GetLatestStatus(this CorrespondenceEntity correspondece)
    {
        var statusEntity = correspondece.Statuses
            .Where(s => s.Status != CorrespondenceStatus.Fetched)
            .OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        return statusEntity;
    }
    public static bool IsPurged(this CorrespondenceStatus correspondenceStatus)
    {
        return correspondenceStatus == CorrespondenceStatus.PurgedByRecipient || correspondenceStatus == CorrespondenceStatus.PurgedByAltinn;
    }
    public static bool IsAvailableForRecipient(this CorrespondenceStatus correspondenceStatus)
    {
        List<CorrespondenceStatus> validStatuses =
        [
            CorrespondenceStatus.Published, CorrespondenceStatus.Read, CorrespondenceStatus.Replied,
            CorrespondenceStatus.Confirmed, CorrespondenceStatus.Archived, CorrespondenceStatus.Reserved
        ];
        return validStatuses.Contains(correspondenceStatus);
    }
}