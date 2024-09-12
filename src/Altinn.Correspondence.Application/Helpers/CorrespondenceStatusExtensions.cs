using Altinn.Correspondence.Core.Models.Enums;

public static class CorrespondenceStatusExtensions
{
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