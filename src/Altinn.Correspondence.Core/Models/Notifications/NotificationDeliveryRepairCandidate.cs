namespace Altinn.Correspondence.Core.Models.Notifications;

public sealed record NotificationDeliveryRepairCandidate(
    Guid NotificationId,
    Guid CorrespondenceId,
    bool IsReminder
);

