using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Application.CreateNotification;

public class CreateNotificationRequest
{
    public Guid CorrespondenceId { get; set; }
    public NotificationTemplate Template { get; set; }

    public NotificationChannel Channel { get; set; }
    public List<NotificationOrderRequest> Notifications { get; set; } = new List<NotificationOrderRequest>();
}
