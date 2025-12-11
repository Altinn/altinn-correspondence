using Altinn.Correspondence.Application.InitializeCorrespondences;

namespace Altinn.Correspondence.Application.CreateNotificationOrder;

public class CreateNotificationOrderRequest
{
    public required NotificationRequest NotificationRequest { get; set; }
    public required Guid CorrespondenceId { get; set; }
    public string? Language { get; set; } = null;
} 