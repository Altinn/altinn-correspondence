using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
namespace Altinn.Correspondence.Application.Helpers;
public static class CorrespondenceStatusExtensions
{
    public static CorrespondenceStatusEntity? GetHighestStatus(this CorrespondenceEntity correspondence)
    {
        var statusEntity = correspondence.Statuses
            .Where(s => s.Status != CorrespondenceStatus.Fetched)
            .OrderByDescending(s => s.Status).FirstOrDefault();
        return statusEntity;
    }
    public static CorrespondenceStatusEntity? GetHighestStatusWithoutPurged(this CorrespondenceEntity correspondence)
    {
        var statusEntity = correspondence.Statuses
            .Where(s => !s.Status.IsPurged() && s.Status != CorrespondenceStatus.Fetched)
            .OrderByDescending(s => s.Status).FirstOrDefault();
        return statusEntity;
    }
    public static bool IsPurged(this CorrespondenceStatus correspondenceStatus)
    {
        return correspondenceStatus == CorrespondenceStatus.PurgedByRecipient || correspondenceStatus == CorrespondenceStatus.PurgedByAltinn;
    }
    public static CorrespondenceStatusEntity? GetPurgedStatus(this CorrespondenceEntity correspondence)
    {
        var statusEntity = correspondence.Statuses
            .Where(s => s.Status.IsPurged())
            .OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        return statusEntity;
    }

    public static bool IsAvailableForSender(this CorrespondenceStatus correspondenceStatus)
    {
        List<CorrespondenceStatus> validStatuses =
        [
            CorrespondenceStatus.Initialized, CorrespondenceStatus.ReadyForPublish, CorrespondenceStatus.Failed,
            CorrespondenceStatus.Published, CorrespondenceStatus.Fetched, CorrespondenceStatus.Read, 
            CorrespondenceStatus.Confirmed, CorrespondenceStatus.Replied, CorrespondenceStatus.Reserved,
            CorrespondenceStatus.AttachmentsDownloaded
        ];
        return validStatuses.Contains(correspondenceStatus);
    }

    public static bool IsAvailableForRecipient(this CorrespondenceStatus correspondenceStatus)
    {
        List<CorrespondenceStatus> validStatuses =
        [
            CorrespondenceStatus.Published, CorrespondenceStatus.Fetched, CorrespondenceStatus.Read, CorrespondenceStatus.Replied,
            CorrespondenceStatus.Confirmed, CorrespondenceStatus.Archived, CorrespondenceStatus.Reserved, CorrespondenceStatus.AttachmentsDownloaded
        ];
        return validStatuses.Contains(correspondenceStatus);
    }
    public static bool IsAvailableForLegacyRecipient(this CorrespondenceStatus correspondenceStatus)
    {
        List<CorrespondenceStatus> validStatuses =
        [
            CorrespondenceStatus.Published, CorrespondenceStatus.Read, CorrespondenceStatus.Replied,
            CorrespondenceStatus.Confirmed, CorrespondenceStatus.Archived, CorrespondenceStatus.Reserved
        ];
        return validStatuses.Contains(correspondenceStatus);
    }
    public static bool IsPurgeableForSender(this CorrespondenceStatus correspondenceStatus)
    {
        List<CorrespondenceStatus> validStatuses =
        [
            CorrespondenceStatus.Initialized, CorrespondenceStatus.ReadyForPublish, CorrespondenceStatus.Failed,
        ];
        return validStatuses.Contains(correspondenceStatus);
    }
    public static bool StatusHasBeen(this CorrespondenceEntity correspondence, CorrespondenceStatus status)
    {
        return correspondence.Statuses.Any(s => s.Status == status);
    }
}