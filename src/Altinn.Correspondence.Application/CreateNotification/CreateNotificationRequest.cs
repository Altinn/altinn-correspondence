using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using static Altinn.Correspondence.Application.CreateNotification.CreateNotificationHandler;

namespace Altinn.Correspondence.Application.CreateNotification;

public class CreateNotificationRequest
{
    public Guid CorrespondenceId { get; set; }
    //public NotificationTemplate Template { get; set; }
    //public NotificationChannel Channel { get; set; }
    public NotificationRequest NotificationRequest { get; set; }
    //public List<NotificationOrderRequest> Notifications { get; set; } = new List<NotificationOrderRequest>();
}
