namespace Altinn.Correspondence.Core.Notifications;

public class ComposedEmailResponse
{
    public required Guid NotificationOrderId { get; set; }

    public required ComposedEmailNotificationResponse Notification { get; set; }
}

public class ComposedEmailNotificationResponse
{
    public required Guid ShipmentId { get; set; }

    public string? SendersReference { get; set; }

    public List<ComposedEmailReminderResponse> Reminders { get; set; } = [];
}

public class ComposedEmailReminderResponse
{
    public required Guid ShipmentId { get; set; }

    public string? SendersReference { get; set; }
}